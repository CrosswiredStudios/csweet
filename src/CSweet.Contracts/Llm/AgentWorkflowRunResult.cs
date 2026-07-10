namespace CSweet.Contracts.Llm;

public sealed record AgentWorkflowRunResult(
    bool Succeeded,
    string? Content,
    string? StructuredJson,
    string? FailureMessage,
    IReadOnlyList<AgentRunLogEntry> Logs);
