namespace CSweet.Contracts.Planning;

public sealed record OrganizationResponse(
    Guid Id,
    string Name,
    string? Industry,
    string? Description,
    string? Stage,
    string? Location,
    string? TeamSize,
    string? AnnualRevenue,
    string? StrategicGoals,
    string? KeyChallenges,
    string? CompetitiveAdvantages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
