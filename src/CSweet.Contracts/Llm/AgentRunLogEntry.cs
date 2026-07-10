namespace CSweet.Contracts.Llm;

public sealed record AgentRunLogEntry(
    string Level,
    string Message,
    DateTimeOffset Timestamp);
