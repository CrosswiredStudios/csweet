using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public static class HiringCapabilities
{
    public const string UpsertRecommendation = "platform.hiring-recommendation.upsert.v1";
    public const string StageWorkflow = "platform.hiring-workflow.stage.v1";
}

public sealed record HiringCandidateResponse(
    string CandidateReference,
    string Source,
    string DisplayName,
    string ResourceType,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> Credentials,
    decimal FitScore,
    decimal? Price,
    string? Currency,
    string Trust,
    bool Available,
    string InstallationState,
    IReadOnlyList<string> RequiredGrants,
    string Rationale);

public sealed record HiringRecommendationResponse(
    Guid Id,
    Guid? WorkstreamId,
    string Title,
    string Objective,
    string Status,
    string RecommendedCandidateReference,
    IReadOnlyList<HiringCandidateResponse> Candidates,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public string HiringUrl { get; init; } = string.Empty;
}

public sealed record UpsertHiringRecommendationRequest(
    [property: Required, MaxLength(256)] string Title,
    [property: Required, MaxLength(2048)] string Objective,
    Guid? WorkstreamId,
    [property: MinLength(1), MaxLength(3)] IReadOnlyList<string> CandidateReferences,
    [property: Required] string RecommendedCandidateReference,
    [property: Required, MaxLength(160)] string IdempotencyKey);

public sealed record StageHiringWorkflowRequest(
    Guid RecommendationId,
    [property: Required] string CandidateReference,
    [property: Required, MaxLength(160)] string RoleTitle,
    Guid? ReportsToOrganizationUserId,
    IReadOnlyList<string>? RequiredGrants,
    [property: Required, MaxLength(160)] string IdempotencyKey);

public sealed record HiringWorkflowResponse(
    Guid Id,
    Guid RecommendationId,
    string CandidateReference,
    string RoleTitle,
    string Status,
    string Message,
    DateTimeOffset CreatedAt,
    Guid? ResultOrganizationUserId = null);

public sealed record ConfirmHiringWorkflowRequest(
    [property: Required, MaxLength(160)] string IdempotencyKey);

public sealed record HiringDashboardResponse(
    IReadOnlyList<HiringRecommendationResponse> Recommendations,
    IReadOnlyList<HiringWorkflowResponse> Workflows);
