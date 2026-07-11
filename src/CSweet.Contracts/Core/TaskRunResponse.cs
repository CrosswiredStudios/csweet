namespace CSweet.Contracts.Core;

public sealed record TaskRunResponse(
    Guid Id,
    Guid TaskId,
    Guid? WorkerId,
    int Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? InputJson,
    string? OutputJson,
    string? FailureMessage,
    decimal? CostAmount,
    string? CostCurrency);
