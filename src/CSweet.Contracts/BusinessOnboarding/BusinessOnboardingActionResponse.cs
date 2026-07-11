namespace CSweet.Contracts.BusinessOnboarding;

public sealed record BusinessOnboardingActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    CompleteBusinessOnboardingResponse? Onboarding = null);
