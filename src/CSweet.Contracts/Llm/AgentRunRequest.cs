namespace CSweet.Contracts.Llm;

public sealed record AgentRunRequest(
    Guid ProviderProfileId,
    string AgentKey,
    string SystemPrompt,
    string UserPrompt,
    IReadOnlyDictionary<string, string> Context,
    AgentRunOptions Options);
