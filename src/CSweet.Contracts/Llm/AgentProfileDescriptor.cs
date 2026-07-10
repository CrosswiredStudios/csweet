namespace CSweet.Contracts.Llm;

public sealed record AgentProfileDescriptor(
    string AgentKey,
    string DisplayName,
    string Description,
    string SystemPrompt,
    IReadOnlyList<string> Capabilities);
