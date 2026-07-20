using System.Text.Json;
using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class HiringService(
    CSweetDbContext db,
    IOrganizationUserService organizationUsers,
    IAuditEventWriter audit) : IHiringService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HiringRecommendationResponse> UpsertRecommendationAsync(
        Guid organizationId,
        Guid requestingInstallationId,
        UpsertHiringRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        var title = Required(request.Title, 256, nameof(request.Title));
        var objective = Required(request.Objective, 2048, nameof(request.Objective));
        var key = Required(request.IdempotencyKey, 160, nameof(request.IdempotencyKey));
        var references = request.CandidateReferences.Distinct(StringComparer.Ordinal).ToList();
        if (references.Count > 3) throw new ArgumentException("A recommendation may contain up to three ranked candidates.");
        if (request.Priority is < 1 or > 100) throw new ArgumentException("Priority must be between 1 and 100, where 1 is highest.");
        if (references.Count == 0 && !string.IsNullOrWhiteSpace(request.RecommendedCandidateReference))
            throw new ArgumentException("A recommendation without candidates cannot select a recommended candidate.");
        if (references.Count > 0 && (string.IsNullOrWhiteSpace(request.RecommendedCandidateReference) ||
            !references.Contains(request.RecommendedCandidateReference, StringComparer.Ordinal)))
            throw new ArgumentException("The recommended candidate must be in the ranked candidate list.");
        var candidates = await ResolveCandidatesAsync(organizationId, references, cancellationToken);
        if (candidates.Count != references.Count) throw new ArgumentException("One or more candidate references are invalid or expired.");
        if (request.WorkstreamId.HasValue && !await db.Workstreams.AsNoTracking().AnyAsync(x =>
                x.Id == request.WorkstreamId && x.OrganizationId == organizationId, cancellationToken))
            throw new ArgumentException("The workstream does not belong to this organization.");

        var existing = await db.WorkforcePlans.SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.RequestingInstallationId == requestingInstallationId && x.IdempotencyKey == key, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var orderedIds = references.Select(reference => ParseCandidateReference(reference)).ToList();
        var recommendedId = string.IsNullOrWhiteSpace(request.RecommendedCandidateReference)
            ? (Guid?)null
            : ParseCandidateReference(request.RecommendedCandidateReference);
        var plan = existing ?? new WorkforcePlan
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, RequestingInstallationId = requestingInstallationId,
            IdempotencyKey = key, CreatedAt = now, Status = ProposalStatus.Pending
        };
        plan.WorkstreamId = request.WorkstreamId;
        plan.Title = title;
        plan.Objective = objective;
        plan.Priority = request.Priority;
        plan.RecommendedCandidateId = recommendedId;
        plan.AssignmentsJson = JsonSerializer.Serialize(orderedIds, JsonOptions);
        var recommendedCandidate = recommendedId.HasValue ? candidates.First(x => x.Id == recommendedId.Value) : null;
        plan.EstimatedMonthlyCost = recommendedCandidate?.EstimatedCost;
        plan.Currency = recommendedCandidate?.Currency;
        plan.UpdatedAt = now;
        foreach (var candidate in candidates) candidate.WorkforcePlanId = plan.Id;
        if (existing is null) db.WorkforcePlans.Add(plan);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("hiring.recommendation.upserted", nameof(WorkforcePlan), plan.Id,
            $"Ranked {candidates.Count} candidates for {title}.", cancellationToken: cancellationToken);
        return ToRecommendation(plan, candidates);
    }

    public async Task<HiringWorkflowResponse> StageWorkflowAsync(
        Guid organizationId,
        Guid requestingInstallationId,
        StageHiringWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        var roleTitle = Required(request.RoleTitle, 160, nameof(request.RoleTitle));
        var key = Required(request.IdempotencyKey, 160, nameof(request.IdempotencyKey));
        var existing = await db.StaffingActionProposals.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.RequestingInstallationId == requestingInstallationId &&
            x.IdempotencyKey == key, cancellationToken);
        if (existing is not null) return ToWorkflow(existing);

        var recommendation = await db.WorkforcePlans.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == request.RecommendationId && x.OrganizationId == organizationId, cancellationToken)
            ?? throw new ArgumentException("The hiring recommendation was not found.");
        var candidateId = ParseCandidateReference(request.CandidateReference);
        if (!ReadIds(recommendation.AssignmentsJson).Contains(candidateId))
            throw new ArgumentException("The selected candidate is not part of this recommendation.");
        var candidate = await db.WorkforceCandidates.AsNoTracking().SingleAsync(x =>
            x.Id == candidateId && x.OrganizationId == organizationId, cancellationToken);
        if (!candidate.IsAvailable) throw new InvalidOperationException("The candidate is no longer available.");
        if (request.ReportsToOrganizationUserId.HasValue && !await db.CoreOrganizationUsers.AsNoTracking().AnyAsync(x =>
                x.Id == request.ReportsToOrganizationUserId && x.OrganizationId == organizationId && x.IsActive,
                cancellationToken))
            throw new ArgumentException("The proposed manager does not belong to this organization.");

        var snapshot = await BuildWorkflowSnapshotAsync(candidate, roleTitle, request.ReportsToOrganizationUserId,
            request.RequiredGrants ?? [], cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var workflow = new StaffingActionProposal
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, WorkforcePlanId = recommendation.Id,
            RequestingInstallationId = requestingInstallationId, IdempotencyKey = key,
            ActionType = "install-and-hire", CandidateSource = candidate.Source,
            CandidateId = request.CandidateReference, PayloadJson = JsonSerializer.Serialize(snapshot, JsonOptions),
            Status = ProposalStatus.Pending, CreatedAt = now
        };
        db.StaffingActionProposals.Add(workflow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("hiring.workflow.staged", nameof(StaffingActionProposal), workflow.Id,
            $"Staged {roleTitle} hiring workflow for owner approval.", cancellationToken: cancellationToken);
        return ToWorkflow(workflow);
    }

    public async Task<IReadOnlyList<HiringRecommendationResponse>> ListRecommendationsAsync(
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var plans = await db.WorkforcePlans.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Priority).ThenBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var candidateIds = plans.SelectMany(x => ReadIds(x.AssignmentsJson)).Distinct().ToList();
        var candidates = await db.WorkforceCandidates.AsNoTracking().Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return plans.Select(plan => ToRecommendation(plan, ReadIds(plan.AssignmentsJson)
            .Where(candidates.ContainsKey).Select(id => candidates[id]).ToList())).ToList();
    }

    public async Task<IReadOnlyList<HiringRecommendationResponse>> ListRecommendationsForInstallationAsync(
        Guid organizationId,
        Guid requestingInstallationId,
        CancellationToken cancellationToken = default)
    {
        var plans = await db.WorkforcePlans.AsNoTracking().Where(x =>
                x.OrganizationId == organizationId && x.RequestingInstallationId == requestingInstallationId)
            .OrderBy(x => x.Priority).ThenBy(x => x.CreatedAt).ToListAsync(cancellationToken);
        var candidateIds = plans.SelectMany(x => ReadIds(x.AssignmentsJson)).Distinct().ToList();
        var candidates = await db.WorkforceCandidates.AsNoTracking().Where(x => candidateIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return plans.Select(plan => ToRecommendation(plan, ReadIds(plan.AssignmentsJson)
            .Where(candidates.ContainsKey).Select(id => candidates[id]).ToList())).ToList();
    }

    public async Task<HiringDashboardResponse> GetDashboardAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var recommendations = await ListRecommendationsAsync(organizationId, cancellationToken);
        var workflows = await db.StaffingActionProposals.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x)
            .ToListAsync(cancellationToken);
        return new(recommendations, workflows.Select(ToWorkflow).ToList());
    }

    public async Task<HiringWorkflowResponse?> ConfirmWorkflowAsync(
        Guid organizationId,
        Guid workflowId,
        Guid applicationUserId,
        ConfirmHiringWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = Required(request.IdempotencyKey, 160, nameof(request.IdempotencyKey));
        var owner = await db.CoreOrganizationUsers.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId && x.ApplicationUserId == applicationUserId && x.IsActive,
            cancellationToken);
        if (owner?.PermissionLevel != OrganizationPermissionLevel.Owner)
            throw new UnauthorizedAccessException("Only an organization owner may approve a hiring workflow.");
        var workflow = await db.StaffingActionProposals.SingleOrDefaultAsync(x =>
            x.Id == workflowId && x.OrganizationId == organizationId, cancellationToken);
        if (workflow is null) return null;
        if (workflow.Status == ProposalStatus.Approved) return ToWorkflow(workflow);
        if (workflow.Status != ProposalStatus.Pending)
            throw new InvalidOperationException("The hiring workflow is no longer pending.");

        var snapshot = JsonSerializer.Deserialize<WorkflowSnapshot>(workflow.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("The hiring workflow snapshot is invalid.");
        var candidateId = ParseCandidateReference(workflow.CandidateId);
        var candidate = await db.WorkforceCandidates.SingleAsync(x => x.Id == candidateId &&
            x.OrganizationId == organizationId, cancellationToken);
        await RevalidateAsync(organizationId, candidate, snapshot, workflowId, cancellationToken);

        var role = await db.CoreRoles.SingleOrDefaultAsync(x => x.OrganizationId == organizationId &&
            x.Name == snapshot.RoleTitle, cancellationToken);
        if (role is null)
        {
            var now = DateTimeOffset.UtcNow;
            role = new Role { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = snapshot.RoleTitle,
                Description = $"Approved through hiring workflow {workflow.Id}.", AuthorityLevel = AuthorityLevel.ExecutionWithApproval,
                CreatedAt = now, UpdatedAt = now };
            db.CoreRoles.Add(role);
            await db.SaveChangesAsync(cancellationToken);
        }

        Guid resultUserId;
        if (candidate.Source == "CurrentStaff")
        {
            var workerId = Guid.Parse(candidate.ExternalCandidateId);
            var employee = await db.CoreOrganizationUsers.SingleAsync(x => x.OrganizationId == organizationId &&
                x.WorkerId == workerId && x.IsActive, cancellationToken);
            employee.RoleId = role.Id;
            if (snapshot.WorkstreamId.HasValue && !await db.Responsibilities.AnyAsync(x =>
                    x.OrganizationUserId == employee.Id && x.WorkstreamId == snapshot.WorkstreamId && x.Status == "Active",
                    cancellationToken))
                db.Responsibilities.Add(new Responsibility { Id = Guid.NewGuid(), OrganizationId = organizationId,
                    OrganizationUserId = employee.Id, WorkstreamId = snapshot.WorkstreamId, Title = snapshot.RoleTitle,
                    Outcome = snapshot.Objective, Status = "Active" });
            await db.SaveChangesAsync(cancellationToken);
            resultUserId = employee.Id;
        }
        else if (candidate.Source == "InstalledPlugin")
        {
            var installationId = Guid.Parse(candidate.ExternalCandidateId);
            var result = await organizationUsers.CreateAsync(organizationId, new CreateOrganizationUserRequest(
                candidate.DisplayName, null, (int)OrganizationPermissionLevel.Contributor, (int)EmployeeType.Agent,
                role.Id, null, snapshot.ReportsToOrganizationUserId, AgentInstallationId: installationId),
                cancellationToken, applicationUserId);
            if (!result.Succeeded || result.OrganizationUser is null)
                throw new InvalidOperationException(result.Message);
            resultUserId = result.OrganizationUser.Id;
        }
        else
        {
            throw new InvalidOperationException("This candidate source cannot be hired until its installation or provider engagement succeeds.");
        }

        workflow.Status = ProposalStatus.Approved;
        workflow.ApprovedByOrganizationUserId = owner.Id;
        workflow.ResultOrganizationUserId = resultUserId;
        workflow.DecidedAt = DateTimeOffset.UtcNow;
        var plan = await db.WorkforcePlans.SingleAsync(x => x.Id == workflow.WorkforcePlanId, cancellationToken);
        plan.Status = ProposalStatus.Approved;
        plan.DecidedAt = workflow.DecidedAt;
        plan.UpdatedAt = workflow.DecidedAt.Value;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("hiring.workflow.approved", nameof(StaffingActionProposal), workflow.Id,
            $"Owner approved and completed the {snapshot.RoleTitle} workflow.", cancellationToken: cancellationToken);
        return ToWorkflow(workflow);
    }

    private async Task<WorkflowSnapshot> BuildWorkflowSnapshotAsync(WorkforceCandidate candidate, string roleTitle,
        Guid? reportsTo, IReadOnlyList<string> requiredGrants, CancellationToken token)
    {
        string? digest = null;
        IReadOnlyList<string> currentGrants = [];
        if (candidate.Source == "InstalledPlugin" && Guid.TryParse(candidate.ExternalCandidateId, out var installationId))
        {
            var installation = await db.AgentInstallations.AsNoTracking().Include(x => x.PackageVersion).Include(x => x.Grant)
                .SingleAsync(x => x.Id == installationId, token);
            digest = installation.PackageVersion?.PackageDigest ?? installation.PackageVersion?.ManifestDigest;
            currentGrants = ReadStrings(installation.Grant?.RequestedCapabilitiesJson);
            if (requiredGrants.Except(currentGrants, StringComparer.Ordinal).Any())
                throw new InvalidOperationException("The installed agent does not currently have all required grants.");
        }
        var planId = candidate.WorkforcePlanId ?? throw new InvalidOperationException(
            "The candidate is not attached to a hiring recommendation.");
        var plan = await db.WorkforcePlans.AsNoTracking().SingleAsync(x => x.Id == planId, token);
        return new(roleTitle, reportsTo, plan.WorkstreamId, plan.Objective, candidate.EstimatedCost, candidate.Currency,
            digest, requiredGrants.Distinct(StringComparer.Ordinal).ToList(), currentGrants);
    }

    private async Task RevalidateAsync(Guid organizationId, WorkforceCandidate candidate, WorkflowSnapshot snapshot,
        Guid workflowId, CancellationToken token)
    {
        if (!candidate.IsAvailable) throw new InvalidOperationException("The candidate is no longer available.");
        var profile = await db.FinancialOperatingProfiles.AsNoTracking().SingleOrDefaultAsync(x =>
            x.OrganizationId == organizationId, token);
        if (profile?.MaximumConcurrentHires is { } cap)
        {
            var pending = await db.StaffingActionProposals.CountAsync(x => x.OrganizationId == organizationId &&
                x.Status == ProposalStatus.Pending && x.Id != workflowId, token);
            if (pending >= cap) throw new InvalidOperationException("The organization's concurrent hiring cap has been reached.");
        }
        if (snapshot.Price is > 0)
        {
            if (profile?.MaximumMonthlyWorkforceSpend is { } max && snapshot.Price > max)
                throw new InvalidOperationException("The candidate price exceeds the workforce spending control.");
            var now = DateTimeOffset.UtcNow;
            var budget = await db.Budgets.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.IsActive &&
                x.ScopeType == BudgetScopeType.Organization && x.PeriodStart <= now && x.PeriodEnd > now &&
                x.Currency == snapshot.Currency).OrderBy(x => x.LimitAmount).FirstOrDefaultAsync(token);
            if (budget is null || snapshot.Price > budget.LimitAmount)
                throw new InvalidOperationException("The hiring workflow no longer fits an active organization budget.");
        }
        if (candidate.Source == "InstalledPlugin")
        {
            var installationId = Guid.Parse(candidate.ExternalCandidateId);
            var current = await db.AgentInstallations.AsNoTracking().Include(x => x.PackageVersion).Include(x => x.Grant)
                .SingleOrDefaultAsync(x => x.Id == installationId && x.IsEnabled &&
                    x.BusinessId == organizationId.ToString(), token)
                ?? throw new InvalidOperationException("The installed agent is no longer available.");
            var digest = current.PackageVersion?.PackageDigest ?? current.PackageVersion?.ManifestDigest;
            if (!string.Equals(digest, snapshot.PackageDigest, StringComparison.Ordinal))
                throw new InvalidOperationException("The agent package digest changed; create a new approval.");
            var grants = ReadStrings(current.Grant?.RequestedCapabilitiesJson);
            if (snapshot.RequiredGrants.Except(grants, StringComparer.Ordinal).Any())
                throw new InvalidOperationException("The approved grants changed; create a new approval.");
        }
        else if (candidate.Source == "CurrentStaff")
        {
            var workerId = Guid.Parse(candidate.ExternalCandidateId);
            var current = await db.CoreWorkers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == workerId && x.IsEnabled &&
                (x.OrganizationId == organizationId || x.OrganizationId == null), token)
                ?? throw new InvalidOperationException("The recommended staff resource is no longer available.");
            if (!await db.CoreOrganizationUsers.AsNoTracking().AnyAsync(x => x.OrganizationId == organizationId &&
                    x.WorkerId == workerId && x.IsActive, token))
                throw new InvalidOperationException("The recommended employee is no longer on current staff.");
            var currentPrice = ReadCost(current.CostModelJson);
            if (currentPrice != snapshot.Price)
                throw new InvalidOperationException("The candidate price changed; create a new approval.");
        }
    }

    private async Task<List<WorkforceCandidate>> ResolveCandidatesAsync(Guid organizationId, IReadOnlyList<string> references,
        CancellationToken token)
    {
        var ids = references.Select(ParseCandidateReference).ToList();
        return await db.WorkforceCandidates.Where(x => x.OrganizationId == organizationId && ids.Contains(x.Id))
            .ToListAsync(token);
    }

    private static HiringRecommendationResponse ToRecommendation(WorkforcePlan plan, IReadOnlyList<WorkforceCandidate> candidates)
    {
        var byId = candidates.ToDictionary(x => x.Id);
        var ordered = ReadIds(plan.AssignmentsJson).Where(byId.ContainsKey).Select(id => ToCandidate(byId[id])).ToList();
        return new(plan.Id, plan.WorkstreamId, plan.Title, plan.Objective, plan.Status.ToString(),
            plan.RecommendedCandidateId.HasValue ? CandidateReference(plan.RecommendedCandidateId.Value) : null,
            ordered, plan.CreatedAt, plan.UpdatedAt)
        {
            Priority = plan.Priority,
            HiringUrl = $"/organizations/{plan.OrganizationId:D}/employees?tab=hiring&recommendation={plan.Id:D}"
        };
    }

    private static HiringCandidateResponse ToCandidate(WorkforceCandidate candidate)
    {
        var metadata = ReadMetadata(candidate.ExplanationJson);
        return new(CandidateReference(candidate.Id), candidate.Source, candidate.DisplayName,
            metadata.ResourceType ?? (candidate.IsHuman ? "Human" : "Agent"), ReadStrings(candidate.CapabilitiesJson),
            metadata.Credentials, candidate.Score, candidate.EstimatedCost, candidate.Currency,
            candidate.Source is "CurrentStaff" or "InstalledPlugin" ? "Platform verified" : "Provider supplied",
            candidate.IsAvailable, candidate.Source == "InstalledPlugin" ? "Installed" : candidate.Source == "CurrentStaff" ? "On staff" : "Not installed",
            metadata.RequiredGrants, metadata.Rationale ?? string.Empty);
    }

    private static HiringWorkflowResponse ToWorkflow(StaffingActionProposal workflow)
    {
        var snapshot = JsonSerializer.Deserialize<WorkflowSnapshot>(workflow.PayloadJson, JsonOptions);
        return new(workflow.Id, workflow.WorkforcePlanId, workflow.CandidateId, snapshot?.RoleTitle ?? "Role",
            workflow.Status.ToString(), workflow.Status == ProposalStatus.Pending ? "Awaiting owner approval." : "Workflow completed.",
            workflow.CreatedAt, workflow.ResultOrganizationUserId);
    }

    private static string CandidateReference(Guid id) => $"candidate:{id:N}";
    private static Guid ParseCandidateReference(string reference) =>
        reference.StartsWith("candidate:", StringComparison.Ordinal) && Guid.TryParseExact(reference[10..], "N", out var id)
            ? id : throw new ArgumentException("The candidate reference is invalid.");
    private static List<Guid> ReadIds(string json) => JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [];
    private static IReadOnlyList<string> ReadStrings(string? json)
    {
        try { return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
    private static decimal? ReadCost(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("amount", out var value) && value.TryGetDecimal(out var amount)
                ? amount : null;
        }
        catch (JsonException) { return null; }
    }
    private static CandidateMetadata ReadMetadata(string json)
    {
        try { return JsonSerializer.Deserialize<CandidateMetadata>(json, JsonOptions) ?? new(); }
        catch (JsonException) { return new(); }
    }
    private static string Required(string? value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.");
        var result = value.Trim();
        if (result.Length > maximum) throw new ArgumentException($"{name} exceeds {maximum} characters.");
        return result;
    }

    private sealed record WorkflowSnapshot(string RoleTitle, Guid? ReportsToOrganizationUserId, Guid? WorkstreamId,
        string Objective, decimal? Price, string? Currency, string? PackageDigest,
        IReadOnlyList<string> RequiredGrants, IReadOnlyList<string> ApprovedGrants);
    private sealed record CandidateMetadata
    {
        public string? ResourceType { get; init; }
        public IReadOnlyList<string> Credentials { get; init; } = [];
        public string? Rationale { get; init; }
        public IReadOnlyList<string> RequiredGrants { get; init; } = [];
    }
}
