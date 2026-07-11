namespace CSweet.Contracts.Core;

public sealed record ApprovalResponse(
    Guid Id,
    Guid ArtifactId,
    int Status,
    string? Comment,
    DateTimeOffset? DecidedAt,
    DateTimeOffset CreatedAt);
