namespace CSweet.Contracts.Llm;

public sealed record AgentRunOptions(
    double? Temperature,
    int? MaxOutputTokens,
    bool RequireStructuredOutput,
    string? OutputSchemaJson);
