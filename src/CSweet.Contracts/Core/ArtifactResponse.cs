namespace CSweet.Contracts.Core;

public sealed record ArtifactResponse(
    Guid Id,
    Guid OrganizationId,
    Guid? TaskId,
    Guid? TaskRunId,
    int Type,
    string Title,
    string Content,
    int Version,
    int ApprovalStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
