namespace CSweet.Contracts.Core;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string? Industry,
    string? Mission,
    string? Stage,
    string? PrimaryGoal,
    string? ConstraintsJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
