namespace CSweet.Contracts.Core;

public sealed record UpdateStrategicObjectiveRequest(
    string? Title,
    string? Description,
    int? Status,
    DateTimeOffset? TargetDate);
