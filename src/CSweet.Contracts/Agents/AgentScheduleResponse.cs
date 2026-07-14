namespace CSweet.Contracts.Agents;

public sealed record AgentScheduleResponse(
    Guid Id,
    string ActivationMode,
    int TickFrequencySeconds,
    DateTimeOffset? NextTickAt,
    DateTimeOffset? LastTickAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? RunRequestedAt,
    int MaxRuntimeSeconds,
    int MaxRetriesPerTick,
    string OverlapPolicy,
    bool IsEnabled);