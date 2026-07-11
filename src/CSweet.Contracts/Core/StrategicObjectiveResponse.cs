namespace CSweet.Contracts.Core;

public sealed record StrategicObjectiveResponse(
    Guid Id,
    Guid OrganizationId,
    string Title,
    string Description,
    int Status,
    DateTimeOffset? TargetDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
