namespace CSweet.Contracts.Llm;

public sealed record AgentRunResult(
    bool Succeeded,
    string? Content,
    string? StructuredJson,
    string? FailureMessage,
    IReadOnlyList<AgentRunLogEntry> Logs);
