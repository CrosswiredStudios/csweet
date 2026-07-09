using CSweet.AI.Providers;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Llm;

public sealed class ModelCatalogClient : IModelCatalogClient
{
    private const string LmStudioApiKeyPlaceholder = "lm-studio";

    private readonly CSweetDbContext _dbContext;
    private readonly ILlmProviderSecretStore _secretStore;
    private readonly OpenAiCompatibleProviderClient _providerClient;

    public ModelCatalogClient(
        CSweetDbContext dbContext,
        ILlmProviderSecretStore secretStore,
        OpenAiCompatibleProviderClient providerClient)
    {
        _dbContext = dbContext;
        _secretStore = secretStore;
        _providerClient = providerClient;
    }

    public async Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == providerProfileId, cancellationToken);

        if (profile is null || !IsOpenAiCompatible(profile.ProviderType) || !IsValidBaseUrl(profile.BaseUrl))
        {
            return [];
        }

        return await _providerClient.ListModelsAsync(profile, await ResolveApiKeyAsync(profile, cancellationToken), cancellationToken);
    }

    private async Task<string> ResolveApiKeyAsync(LlmProviderProfile profile, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(profile.ApiKeySecretName))
        {
            var apiKey = await _secretStore.GetAsync(profile.ApiKeySecretName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                return apiKey;
            }
        }

        return profile.ProviderType == LlmProviderType.LmStudio
            ? LmStudioApiKeyPlaceholder
            : string.Empty;
    }

    private static bool IsOpenAiCompatible(LlmProviderType providerType)
    {
        return providerType is LlmProviderType.LmStudio or LlmProviderType.OpenAiCompatible or LlmProviderType.OpenAi;
    }

    private static bool IsValidBaseUrl(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";
    }
}
