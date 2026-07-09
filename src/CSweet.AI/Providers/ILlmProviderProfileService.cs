using CSweet.Contracts.Llm;

namespace CSweet.AI.Providers;

public interface ILlmProviderProfileService
{
    Task<LlmProviderProfileActionResponse> CreateAsync(
        CreateLlmProviderProfileRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LlmProviderProfileResponse>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<LlmProviderProfileResponse?> GetAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<ModelCapabilityTestResult> TestAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<LlmProviderProfileActionResponse> SetDefaultChatProviderAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}
