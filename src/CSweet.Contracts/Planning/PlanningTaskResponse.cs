namespace CSweet.Contracts.Planning;

public sealed record PlanningTaskResponse(
    Guid Id,
    string TaskKey,
    string DisplayName,
    string Status,
    string? OutputPreview,
    string? FailureMessage,
    int? InputTokenCount,
    int? OutputTokenCount,
    long? DurationMs,
    int SortOrder,
    bool IsRequired,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);
