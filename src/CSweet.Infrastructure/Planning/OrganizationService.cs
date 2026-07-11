using CSweet.Application.Planning;
using CSweet.Contracts.Planning;
using CSweet.Domain.Planning;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Planning;

public sealed class OrganizationService : IOrganizationService
{
    private readonly CSweetDbContext _dbContext;

    public OrganizationService(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Organization>()
            .OrderBy(x => x.Name)
            .Select(x => x.ToResponse())
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Set<Organization>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return org?.ToResponse();
    }

    public async Task<PlanningActionResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("validation_error", "Organization name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Industry = TrimOrNull(request.Industry),
            Description = TrimOrNull(request.Description),
            Stage = TrimOrNull(request.Stage),
            Location = TrimOrNull(request.Location),
            TeamSize = TrimOrNull(request.TeamSize),
            AnnualRevenue = TrimOrNull(request.AnnualRevenue),
            StrategicGoals = TrimOrNull(request.StrategicGoals),
            KeyChallenges = TrimOrNull(request.KeyChallenges),
            CompetitiveAdvantages = TrimOrNull(request.CompetitiveAdvantages),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Set<Organization>().Add(org);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanningActionResponse(true, null, null, org.ToResponse());
    }

    public async Task<PlanningActionResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Set<Organization>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (org is null)
        {
            return Failure("not_found", "Organization was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            org.Name = request.Name.Trim();
        if (request.Industry is not null)
            org.Industry = TrimOrNull(request.Industry);
        if (request.Description is not null)
            org.Description = TrimOrNull(request.Description);
        if (request.Stage is not null)
            org.Stage = TrimOrNull(request.Stage);
        if (request.Location is not null)
            org.Location = TrimOrNull(request.Location);
        if (request.TeamSize is not null)
            org.TeamSize = TrimOrNull(request.TeamSize);
        if (request.AnnualRevenue is not null)
            org.AnnualRevenue = TrimOrNull(request.AnnualRevenue);
        if (request.StrategicGoals is not null)
            org.StrategicGoals = TrimOrNull(request.StrategicGoals);
        if (request.KeyChallenges is not null)
            org.KeyChallenges = TrimOrNull(request.KeyChallenges);
        if (request.CompetitiveAdvantages is not null)
            org.CompetitiveAdvantages = TrimOrNull(request.CompetitiveAdvantages);

        org.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanningActionResponse(true, null, null, org.ToResponse());
    }

    public async Task<PlanningActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _dbContext.Set<Organization>()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (org is null)
        {
            return Failure("not_found", "Organization was not found.");
        }

        _dbContext.Set<Organization>().Remove(org);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PlanningActionResponse(true, null, "Organization deleted successfully.");
    }

    static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    static PlanningActionResponse Failure(string errorCode, string message) =>
        new PlanningActionResponse(false, errorCode, message);
}
