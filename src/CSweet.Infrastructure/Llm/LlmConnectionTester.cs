using System.Net.Http;
using System.Text.Json;
using CSweet.AI.Providers;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Llm;

public sealed class LlmConnectionTester : ILlmConnectionTester
{
    private const string ReadyPrompt = "Return the word READY and nothing else.";
    private const string StructuredPrompt = "Return only a JSON object with this exact shape: {\"status\":\"ready\"}";
    private const string LmStudioApiKeyPlaceholder = "lm-studio";
    private static readonly TimeSpan OptionalCapabilityTimeout = TimeSpan.FromSeconds(8);

    private readonly CSweetDbContext _dbContext;
    private readonly ILlmProviderSecretStore _secretStore;
    private readonly OpenAiCompatibleProviderClient _providerClient;

    public LlmConnectionTester(
        CSweetDbContext dbContext,
        ILlmProviderSecretStore secretStore,
        OpenAiCompatibleProviderClient providerClient)
    {
        _dbContext = dbContext;
        _secretStore = secretStore;
        _providerClient = providerClient;
    }

    public async Task<ModelCapabilityTestResult> TestAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.LlmProviderProfiles
            .SingleOrDefaultAsync(x => x.Id == providerProfileId, cancellationToken);

        if (profile is null)
        {
            return new ModelCapabilityTestResult(providerProfileId, false, false, false, false, false, "Provider profile was not found.");
        }

        if (!IsValidBaseUrl(profile.BaseUrl))
        {
            var invalidResult = new ModelCapabilityTestResult(profile.Id, false, false, false, false, false, "Invalid base URL.");
            await PersistAsync(profile, invalidResult, rawResult: null, cancellationToken);
            return invalidResult;
        }

        if (!IsOpenAiCompatible(profile.ProviderType))
        {
            var unsupportedResult = new ModelCapabilityTestResult(profile.Id, false, false, false, false, false, "Unsupported provider type.");
            await PersistAsync(profile, unsupportedResult, rawResult: null, cancellationToken);
            return unsupportedResult;
        }

        try
        {
            var apiKey = await ResolveApiKeyAsync(profile, cancellationToken);
            var models = await _providerClient.ListModelsAsync(profile, apiKey, cancellationToken);

            if (models.Count > 0 &&
                !models.Any(x => string.Equals(x.Id, profile.DefaultChatModel, StringComparison.OrdinalIgnoreCase)))
            {
                var modelMissingResult = new ModelCapabilityTestResult(profile.Id, true, false, false, false, false, "Model missing.");
                await PersistAsync(profile, modelMissingResult, JsonSerializer.Serialize(new { models }), cancellationToken);
                return modelMissingResult;
            }

            var chatResponse = await _providerClient.CompleteChatAsync(profile, apiKey, ReadyPrompt, cancellationToken);
            var chatSucceeded = string.Equals(chatResponse.Trim(), "READY", StringComparison.OrdinalIgnoreCase);

            if (!chatSucceeded)
            {
                var failedChatResult = new ModelCapabilityTestResult(profile.Id, true, false, false, false, false, "Chat completion failed.");
                await PersistAsync(profile, failedChatResult, JsonSerializer.Serialize(new { chatResponse }), cancellationToken);
                return failedChatResult;
            }

            var streamingSucceeded = false;
            if (profile.SupportsStreaming)
            {
                streamingSucceeded = await TryCapabilityAsync(
                    optionalCancellationToken => _providerClient.CompleteStreamingChatAsync(profile, apiKey, optionalCancellationToken),
                    cancellationToken);
            }

            var structuredOutputSucceeded = false;
            if (profile.SupportsStructuredOutput)
            {
                structuredOutputSucceeded = await TryCapabilityAsync(async optionalCancellationToken =>
                {
                    var structuredResponse = await _providerClient.CompleteChatAsync(profile, apiKey, StructuredPrompt, optionalCancellationToken);
                    return IsReadyJson(structuredResponse);
                }, cancellationToken);
            }

            var toolCallingSucceeded = false;
            if (profile.SupportsToolCalling)
            {
                toolCallingSucceeded = await TryCapabilityAsync(
                    optionalCancellationToken => _providerClient.RequestToolCallAsync(profile, apiKey, optionalCancellationToken),
                    cancellationToken);
            }

            var result = new ModelCapabilityTestResult(
                profile.Id,
                ConnectionSucceeded: true,
                ChatSucceeded: true,
                StreamingSucceeded: streamingSucceeded,
                StructuredOutputSucceeded: structuredOutputSucceeded,
                ToolCallingSucceeded: toolCallingSucceeded,
                FailureMessage: null);

            await PersistAsync(
                profile,
                result,
                JsonSerializer.Serialize(new { models, chatResponse }),
                cancellationToken);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            var timeoutResult = new ModelCapabilityTestResult(profile.Id, false, false, false, false, false, "Timeout.");
            await PersistAsync(profile, timeoutResult, rawResult: null, CancellationToken.None);
            return timeoutResult;
        }
        catch (HttpRequestException)
        {
            var unreachableResult = new ModelCapabilityTestResult(profile.Id, false, false, false, false, false, "Provider unreachable.");
            await PersistAsync(profile, unreachableResult, rawResult: null, cancellationToken);
            return unreachableResult;
        }
        catch (LlmProviderHttpException ex)
        {
            var result = new ModelCapabilityTestResult(profile.Id, false, false, false, false, false, ex.Message);
            await PersistAsync(profile, result, rawResult: null, cancellationToken);
            return result;
        }
        catch (JsonException)
        {
            var result = new ModelCapabilityTestResult(profile.Id, true, false, false, false, false, "Chat completion failed.");
            await PersistAsync(profile, result, rawResult: null, cancellationToken);
            return result;
        }
    }

    private async Task PersistAsync(
        LlmProviderProfile profile,
        ModelCapabilityTestResult result,
        string? rawResult,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        _dbContext.ModelCapabilityTests.Add(new ModelCapabilityTest
        {
            Id = Guid.NewGuid(),
            ProviderProfileId = profile.Id,
            ConnectionSucceeded = result.ConnectionSucceeded,
            ChatSucceeded = result.ChatSucceeded,
            StreamingSucceeded = result.StreamingSucceeded,
            StructuredOutputSucceeded = result.StructuredOutputSucceeded,
            ToolCallingSucceeded = result.ToolCallingSucceeded,
            FailureMessage = result.FailureMessage,
            RawResult = rawResult,
            TestedAt = now
        });

        profile.SupportsStreaming = result.StreamingSucceeded;
        profile.SupportsStructuredOutput = result.StructuredOutputSucceeded;
        profile.SupportsToolCalling = result.ToolCallingSucceeded;
        profile.UpdatedAt = now;

        if (result.ConnectionSucceeded && result.ChatSucceeded)
        {
            profile.LastSuccessfulConnectionAt = now;
        }

        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = "llm_provider_profile.capability_tested",
            EntityType = nameof(LlmProviderProfile),
            EntityId = profile.Id,
            Summary = result.ChatSucceeded
                ? $"LLM provider capability test succeeded: {profile.Name}"
                : $"LLM provider capability test failed: {profile.Name}",
            MetadataJson = JsonSerializer.Serialize(new
            {
                result.ConnectionSucceeded,
                result.ChatSucceeded,
                result.StreamingSucceeded,
                result.StructuredOutputSucceeded,
                result.ToolCallingSucceeded
            }),
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
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

    private static async Task<bool> TryCapabilityAsync(
        Func<CancellationToken, Task<bool>> capabilityTest,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(OptionalCapabilityTimeout);

        try
        {
            return await capabilityTest(timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or LlmProviderHttpException or JsonException or OperationCanceledException)
        {
            return false;
        }
    }

    private static bool IsReadyJson(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("status", out var status) &&
                string.Equals(status.GetString(), "ready", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
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
