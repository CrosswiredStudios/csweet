using CSweet.Contracts.Llm;

namespace CSweet.App.Services;

public interface ILlmProviderApiClient
{
    Task<IReadOnlyList<LlmProviderProfileResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<PreviewModelCatalogResponse> PreviewModelCatalogAsync(PreviewModelCatalogRequest request, CancellationToken cancellationToken = default);
    Task<LlmProviderProfileResponse> CreateAsync(CreateLlmProviderProfileRequest request, CancellationToken cancellationToken = default);
    Task<ModelCapabilityTestResult> TestAsync(Guid providerProfileId, CancellationToken cancellationToken = default);
    Task<LlmProviderProfileActionResponse> SetDefaultChatProviderAsync(Guid providerProfileId, CancellationToken cancellationToken = default);
}
