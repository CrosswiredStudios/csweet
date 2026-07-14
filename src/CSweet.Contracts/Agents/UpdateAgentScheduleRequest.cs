namespace CSweet.Contracts.Agents;

public sealed record UpdateAgentScheduleRequest(
    string ActivationMode,
    int TickFrequencySeconds,
    string OverlapPolicy,
    int MaxRuntimeSeconds,
    bool IsEnabled);