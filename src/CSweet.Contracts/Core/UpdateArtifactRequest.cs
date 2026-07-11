namespace CSweet.Contracts.Core;

public sealed record UpdateArtifactRequest(
    string? Title,
    string? Content,
    int? Version,
    int? ApprovalStatus);
