using CSweet.AI.Providers;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace CSweet.Infrastructure.Llm;

public sealed class OpenAiCompatibleLlmProviderFactory : ILlmProviderFactory
{
    private const string LmStudioApiKeyPlaceholder = "lm-studio";

    private readonly CSweetDbContext _dbContext;
    private readonly ILlmProviderSecretStore _secretStore;

    public OpenAiCompatibleLlmProviderFactory(
        CSweetDbContext dbContext,
        ILlmProviderSecretStore secretStore)
    {
        _dbContext = dbContext;
        _secretStore = secretStore;
    }

    public async Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == providerProfileId, cancellationToken);

        if (profile is null)
        {
            throw new InvalidOperationException("Provider profile was not found.");
        }

        if (!IsOpenAiCompatible(profile.ProviderType))
        {
            throw new NotSupportedException($"Provider type '{profile.ProviderType}' is not supported by the OpenAI-compatible factory.");
        }

        if (!Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Provider base URL is invalid.");
        }

        var apiKey = await ResolveApiKeyAsync(profile, cancellationToken);
        var options = new OpenAIClientOptions { Endpoint = endpoint };
        var chatClient = new ChatClient(profile.DefaultChatModel, new ApiKeyCredential(apiKey), options);

        return chatClient.AsIChatClient();
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
}
