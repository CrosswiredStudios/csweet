using CSweet.AI.Providers;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Llm;

public sealed class LlmProviderProfileService : ILlmProviderProfileService
{
    private readonly CSweetDbContext _dbContext;
    private readonly ILlmProviderSecretStore _secretStore;
    private readonly ILlmConnectionTester _connectionTester;

    public LlmProviderProfileService(
        CSweetDbContext dbContext,
        ILlmProviderSecretStore secretStore,
        ILlmConnectionTester connectionTester)
    {
        _dbContext = dbContext;
        _secretStore = secretStore;
        _connectionTester = connectionTester;
    }

    public async Task<LlmProviderProfileActionResponse> CreateAsync(
        CreateLlmProviderProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Failure("validation_error", "Provider name is required.");
        }

        if (!IsValidBaseUrl(request.BaseUrl))
        {
            return Failure("invalid_base_url", "Provider base URL must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(request.DefaultChatModel))
        {
            return Failure("validation_error", "Default chat model is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var profileId = Guid.NewGuid();
        string? apiKeySecretName = null;

        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            apiKeySecretName = $"llm-provider-profiles/{profileId}/api-key";
            await _secretStore.StoreAsync(apiKeySecretName, request.ApiKey, cancellationToken);
        }

        var profile = new LlmProviderProfile
        {
            Id = profileId,
            Name = request.Name.Trim(),
            ProviderType = request.ProviderType,
            BaseUrl = request.BaseUrl.Trim(),
            ApiKeySecretName = apiKeySecretName,
            DefaultChatModel = request.DefaultChatModel.Trim(),
            DefaultEmbeddingModel = string.IsNullOrWhiteSpace(request.DefaultEmbeddingModel)
                ? null
                : request.DefaultEmbeddingModel.Trim(),
            ContextWindowTokens = request.ContextWindowTokens,
            MaxOutputTokens = request.MaxOutputTokens,
            SupportsStreaming = request.SupportsStreaming,
            SupportsToolCalling = request.SupportsToolCalling,
            SupportsStructuredOutput = request.SupportsStructuredOutput,
            SupportsVision = request.SupportsVision,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.LlmProviderProfiles.Add(profile);
        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = "llm_provider_profile.created",
            EntityType = nameof(LlmProviderProfile),
            EntityId = profile.Id,
            Summary = $"LLM provider profile created: {profile.Name}",
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LlmProviderProfileActionResponse(true, null, null, profile.ToResponse());
    }

    public async Task<IReadOnlyList<LlmProviderProfileResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _dbContext.LlmProviderProfiles
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return profiles.Select(x => x.ToResponse()).ToList();
    }

    public async Task<LlmProviderProfileResponse?> GetAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == providerProfileId, cancellationToken);

        return profile?.ToResponse();
    }

    public Task<ModelCapabilityTestResult> TestAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        return _connectionTester.TestAsync(providerProfileId, cancellationToken);
    }

    public async Task<LlmProviderProfileActionResponse> SetDefaultChatProviderAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == providerProfileId, cancellationToken);

        if (profile is null)
        {
            return Failure("provider_profile_not_found", "Provider profile was not found.");
        }

        if (!profile.IsEnabled)
        {
            return Failure("provider_profile_disabled", "Provider profile must be enabled before it can be selected.");
        }

        var hasSuccessfulChatTest = await _dbContext.ModelCapabilityTests
            .AnyAsync(x => x.ProviderProfileId == providerProfileId && x.ChatSucceeded, cancellationToken);

        if (!hasSuccessfulChatTest)
        {
            return Failure("successful_chat_test_required", "Provider profile must have a successful chat capability test.");
        }

        var configuration = await _dbContext.SystemConfigurations
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (configuration is null)
        {
            configuration = new SystemConfiguration
            {
                Id = Guid.NewGuid(),
                IsFirstRunComplete = false,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.SystemConfigurations.Add(configuration);
        }

        configuration.DefaultChatProviderId = providerProfileId;
        configuration.UpdatedAt = now;

        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = "setup.default_chat_provider.selected",
            EntityType = nameof(LlmProviderProfile),
            EntityId = providerProfileId,
            Summary = $"Default chat provider selected: {profile.Name}",
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LlmProviderProfileActionResponse(true, null, null, profile.ToResponse());
    }

    private static LlmProviderProfileActionResponse Failure(string errorCode, string message)
    {
        return new LlmProviderProfileActionResponse(false, errorCode, message);
    }

    private static bool IsValidBaseUrl(string baseUrl)
    {
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https";
    }
}
