using CSweet.AI.AgentFramework;
using CSweet.AI.Providers;
using CSweet.Application.Llm;
using CSweet.Application.Planning;
using CSweet.Contracts.Llm;
using CSweet.Contracts.Planning;
using CSweet.Domain.Planning;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Planning;

public sealed class PlanningDocumentService : IPlanningDocumentService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IAgentRunner _agentRunner;

    public PlanningDocumentService(CSweetDbContext dbContext, IAgentRunner agentRunner)
    {
        _dbContext = dbContext;
        _agentRunner = agentRunner;
    }

    public async Task<IReadOnlyList<PlanningDocumentResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<PlanningDocument>()
            .Where(d => d.OrganizationId == organizationId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => d.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanningDocumentResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _dbContext.Set<PlanningDocument>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return doc?.ToResponse();
    }

    public async Task<PlanningDocumentResponse?> GetLatestByTypeAsync(Guid organizationId, string documentType, CancellationToken cancellationToken = default)
    {
        var doc = await _dbContext.Set<PlanningDocument>()
            .Where(d => d.OrganizationId == organizationId && d.DocumentType == documentType && d.IsLatest)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return doc?.ToResponse();
    }

    public async Task<PlanningActionResponse> GenerateAsync(GeneratePlanningDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Set<Organization>()
            .SingleOrDefaultAsync(x => x.Id == request.OrganizationId, cancellationToken);

        if (org is null)
            return Failure("organization_not_found", "Organization was not found.");

        var prompt = BuildGenerationPrompt(request.DocumentType, org);

        var agentRequest = new AgentRunRequest(
            ProviderProfileId: request.ProviderProfileId,
            AgentKey: "business-strategist",
            SystemPrompt: BusinessStrategistAgentProfile.SystemPrompt,
            UserPrompt: prompt,
            Context: BuildOrganizationContext(org),
            Options: new AgentRunOptions(
                Temperature: 0.7,
                MaxOutputTokens: 16384,
                RequireStructuredOutput: false,
                OutputSchemaJson: null));

        var result = await _agentRunner.RunAsync(agentRequest, cancellationToken);

        if (!result.Succeeded)
            return Failure("generation_failed", result.FailureMessage ?? "Document generation failed.");

        // Mark previous versions as not latest
        var previousVersions = await _dbContext.Set<PlanningDocument>()
            .Where(d => d.OrganizationId == request.OrganizationId && d.DocumentType == request.DocumentType && d.IsLatest)
            .ToListAsync(cancellationToken);

        foreach (var prev in previousVersions)
            prev.IsLatest = false;

        var document = new PlanningDocument
        {
            Id = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            Title = DocumentTypeToTitle(request.DocumentType),
            DocumentType = request.DocumentType,
            Content = result.Content ?? string.Empty,
            StructuredJson = result.StructuredJson,
            Summary = Truncate(result.Content, 500),
            Version = previousVersions.Count + 1,
            IsLatest = true,
            GeneratedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Set<PlanningDocument>().Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanningActionResponse(true, null, null, null, null, document.ToResponse());
    }

    public async Task<PlanningActionResponse> UpdateContentAsync(Guid id, string content, CancellationToken cancellationToken = default)
    {
        var doc = await _dbContext.Set<PlanningDocument>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc is null)
            return Failure("not_found", "Document was not found.");

        doc.Content = content;
        doc.Summary = Truncate(content, 500);
        doc.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new PlanningActionResponse(true, null, null, null, null, doc.ToResponse());
    }

    public async Task<PlanningActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var doc = await _dbContext.Set<PlanningDocument>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (doc is null)
            return Failure("not_found", "Document was not found.");

        _dbContext.Set<PlanningDocument>().Remove(doc);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanningActionResponse(true, null, "Document deleted successfully.");
    }

    #region Private Helpers

    static PlanningActionResponse Failure(string errorCode, string message) =>
        new PlanningActionResponse(false, errorCode, message);

    static string BuildGenerationPrompt(string documentType, Organization org)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"Generate a comprehensive {DocumentTypeToTitle(documentType)} for {org.Name}.");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(org.Description))
            sb.AppendLine($"Organization description: {org.Description}");
        if (!string.IsNullOrWhiteSpace(org.Industry))
            sb.AppendLine($"Industry: {org.Industry}");
        if (!string.IsNullOrWhiteSpace(org.Stage))
            sb.AppendLine($"Stage: {org.Stage}");
        if (!string.IsNullOrWhiteSpace(org.Location))
            sb.AppendLine($"Location: {org.Location}");
        if (!string.IsNullOrWhiteSpace(org.TeamSize))
            sb.AppendLine($"Team size: {org.TeamSize}");
        if (!string.IsNullOrWhiteSpace(org.AnnualRevenue))
            sb.AppendLine($"Annual revenue: {org.AnnualRevenue}");
        if (!string.IsNullOrWhiteSpace(org.StrategicGoals))
            sb.AppendLine($"Strategic goals: {org.StrategicGoals}");
        if (!string.IsNullOrWhiteSpace(org.KeyChallenges))
            sb.AppendLine($"Key challenges: {org.KeyChallenges}");
        if (!string.IsNullOrWhiteSpace(org.CompetitiveAdvantages))
            sb.AppendLine($"Competitive advantages: {org.CompetitiveAdvantages}");

        sb.AppendLine();
        sb.AppendLine("Provide a well-structured, professional document with clear sections, actionable insights, and practical recommendations.");

        return sb.ToString();
    }

    static Dictionary<string, string> BuildOrganizationContext(Organization org)
    {
        var context = new Dictionary<string, string> { ["OrganizationName"] = org.Name };
        if (!string.IsNullOrWhiteSpace(org.Industry)) context["Industry"] = org.Industry;
        if (!string.IsNullOrWhiteSpace(org.Stage)) context["Stage"] = org.Stage;
        if (!string.IsNullOrWhiteSpace(org.Location)) context["Location"] = org.Location;
        if (!string.IsNullOrWhiteSpace(org.TeamSize)) context["TeamSize"] = org.TeamSize;
        if (!string.IsNullOrWhiteSpace(org.AnnualRevenue)) context["AnnualRevenue"] = org.AnnualRevenue;
        if (!string.IsNullOrWhiteSpace(org.StrategicGoals)) context["StrategicGoals"] = org.StrategicGoals;
        if (!string.IsNullOrWhiteSpace(org.KeyChallenges)) context["KeyChallenges"] = org.KeyChallenges;
        if (!string.IsNullOrWhiteSpace(org.CompetitiveAdvantages)) context["CompetitiveAdvantages"] = org.CompetitiveAdvantages;
        return context;
    }

    static string DocumentTypeToTitle(string documentType)
    {
        return documentType.Replace("-", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0]) + w[1..])
            .Aggregate((a, b) => a + " " + b);
    }

    static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    #endregion
}
