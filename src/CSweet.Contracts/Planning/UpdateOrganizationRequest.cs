namespace CSweet.Contracts.Planning;

public sealed record UpdateOrganizationRequest(
    string? Name,
    string? Industry,
    string? Description,
    string? Stage,
    string? Location,
    string? TeamSize,
    string? AnnualRevenue,
    string? StrategicGoals,
    string? KeyChallenges,
    string? CompetitiveAdvantages);
