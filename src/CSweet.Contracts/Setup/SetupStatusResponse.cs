namespace CSweet.Contracts.Setup;

public sealed record SetupStatusResponse(
    bool IsFirstRunComplete,
    Guid? DefaultChatProviderId,
    Guid? DefaultEmbeddingProviderId,
    IReadOnlyList<OnboardingStepStatusDto> Steps);

public sealed record OnboardingStepStatusDto(
    string Key,
    string DisplayName,
    bool IsRequired,
    bool IsComplete);
