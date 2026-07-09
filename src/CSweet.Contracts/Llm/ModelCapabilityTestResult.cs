namespace CSweet.Contracts.Llm;

public sealed record ModelCapabilityTestResult(
    Guid ProviderProfileId,
    bool ConnectionSucceeded,
    bool ChatSucceeded,
    bool StreamingSucceeded,
    bool StructuredOutputSucceeded,
    bool ToolCallingSucceeded,
    string? FailureMessage);
