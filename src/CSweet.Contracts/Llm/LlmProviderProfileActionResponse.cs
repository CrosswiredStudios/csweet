namespace CSweet.Contracts.Llm;

public sealed record LlmProviderProfileActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    LlmProviderProfileResponse? Profile = null,
    ModelCapabilityTestResult? TestResult = null);
