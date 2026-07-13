namespace CSweet.Contracts.Llm;

public sealed record LlmTokenUsageSummaryResponse(
    DateTimeOffset GeneratedAt,
    LlmTokenUsageWindowResponse Last24Hours,
    LlmTokenUsageWindowResponse Last7Days,
    LlmTokenUsageWindowResponse Last30Days,
    IReadOnlyList<LlmProviderTokenUsageResponse> Providers,
    IReadOnlyList<AgentTokenUsageResponse> Agents);

public sealed record LlmTokenUsageWindowResponse(
    string Label,
    int RequestCount,
    long InputTokens,
    long OutputTokens,
    long TotalTokens);

public sealed record LlmProviderTokenUsageResponse(
    Guid ProviderProfileId,
    string ProviderName,
    LlmTokenUsageWindowResponse Usage);

public sealed record AgentTokenUsageResponse(
    string AgentKey,
    LlmTokenUsageWindowResponse Usage);
