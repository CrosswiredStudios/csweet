using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AI.Providers;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace CSweet.AgentHost.Broker;

public sealed class PlatformLlmCapabilityHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CSweetDbContext _dbContext;
    private readonly ILlmProviderFactory _providerFactory;
    private readonly ILogger<PlatformLlmCapabilityHandler> _logger;

    public PlatformLlmCapabilityHandler(
        CSweetDbContext dbContext,
        ILlmProviderFactory providerFactory,
        ILogger<PlatformLlmCapabilityHandler> logger)
    {
        _dbContext = dbContext;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async IAsyncEnumerable<CapabilityResult> StreamAsync(
        AgentSession session,
        RequestCapability request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Payload.Length > 1_048_576)
        {
            yield return Failure(request.RequestId, "The LLM request exceeds the 1 MB limit.");
            yield break;
        }

        if (!session.Grant.Permissions.Contains("capability.request"))
        {
            yield return Failure(request.RequestId, "The installation is not granted capability.request.");
            yield break;
        }

        BrokerLlmRequest? input = null;
        var parseFailed = false;
        try
        {
            input = JsonSerializer.Deserialize<BrokerLlmRequest>(request.Payload.Span, JsonOptions);
        }
        catch (JsonException)
        {
            parseFailed = true;
        }

        if (parseFailed)
        {
            yield return Failure(request.RequestId, "The LLM request payload is not valid JSON.");
            yield break;
        }

        if (input is null || input.ProviderProfileId == Guid.Empty || input.Messages.Count == 0)
        {
            yield return Failure(request.RequestId, "The LLM request requires a provider and at least one message.");
            yield break;
        }

        if (input.Messages.Count > 128 || input.Messages.Sum(message => message.Text.Length) > 262_144)
        {
            yield return Failure(request.RequestId, "The LLM request exceeds the message or text limit.");
            yield break;
        }

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromMinutes(2));
        var requestToken = requestTimeout.Token;

        var profile = await _dbContext.LlmProviderProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == input.ProviderProfileId && x.IsEnabled, requestToken);
        if (profile is null)
        {
            yield return Failure(request.RequestId, "The selected LLM provider is unavailable.");
            yield break;
        }

        var selectedModel = string.IsNullOrWhiteSpace(input.Model)
            ? profile.DefaultChatModel
            : input.Model.Trim();
        if (string.IsNullOrWhiteSpace(selectedModel))
        {
            yield return Failure(request.RequestId, "No model is configured for this LLM request.");
            yield break;
        }

        if (!await IsModelApprovedAsync(
                session,
                input.ProviderProfileId,
                selectedModel,
                profile.DefaultChatModel,
                requestToken))
        {
            yield return Failure(request.RequestId, "The selected model is not approved for this provider profile.");
            yield break;
        }

        var messages = input.Messages.Select(message => new ChatMessage(
            ParseRole(message.Role),
            message.Text)).ToList();
        var sequence = 0;

        IAsyncEnumerator<ChatResponseUpdate>? updates = null;
        string? providerError = null;
        try
        {
            var chatClient = await _providerFactory.CreateChatClientAsync(
                input.ProviderProfileId,
                selectedModel,
                requestToken);
            updates = chatClient.GetStreamingResponseAsync(
                messages,
                options: null,
                requestToken).GetAsyncEnumerator(requestToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
        {
            providerError = "The platform LLM request timed out.";
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Brokered LLM request {RequestId} failed for agent {AgentId}.",
                request.RequestId,
                session.AgentId);
            providerError = "The platform LLM provider could not complete the request.";
        }

        if (providerError is not null || updates is null)
        {
            yield return Failure(request.RequestId, providerError ?? "The platform LLM provider could not start the request.");
            yield break;
        }

        await using (updates)
        {
            while (true)
            {
                ChatResponseUpdate? update = null;
                var moved = false;
                try
                {
                    moved = await updates.MoveNextAsync();
                    if (moved)
                    {
                        update = updates.Current;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
                {
                    providerError = "The platform LLM request timed out.";
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Brokered LLM stream {RequestId} failed for agent {AgentId}.",
                        request.RequestId,
                        session.AgentId);
                    providerError = "The platform LLM provider could not complete the request.";
                }

                if (providerError is not null)
                {
                    yield return Failure(request.RequestId, providerError);
                    yield break;
                }

                if (!moved || update is null)
                {
                    break;
                }

                var usage = update.Contents.OfType<UsageContent>().FirstOrDefault()?.Details;
                var chunk = new BrokerLlmChunk(
                    update.Text,
                    usage?.InputTokenCount,
                    usage?.OutputTokenCount);
                yield return Success(request.RequestId, chunk, sequence++, hasMore: true);
            }
        }

        yield return Success(request.RequestId, new BrokerLlmChunk(null), sequence, hasMore: false);
    }

    private async Task<bool> IsModelApprovedAsync(
        AgentSession session,
        Guid providerProfileId,
        string selectedModel,
        string defaultModel,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(defaultModel) &&
            string.Equals(selectedModel, defaultModel, StringComparison.Ordinal))
        {
            return true;
        }

        if (!Guid.TryParse(session.InstallationId, out var installationId))
        {
            return false;
        }

        var settingsJson = await _dbContext.AgentInstallationConfigurations
            .AsNoTracking()
            .Where(x => x.AgentInstallationId == installationId)
            .Select(x => x.SettingsJson)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settingsJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(settingsJson);
            var root = document.RootElement;
            return root.TryGetProperty("llmProviderId", out var providerElement) &&
                providerElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(providerElement.GetString(), out var configuredProviderId) &&
                configuredProviderId == providerProfileId &&
                root.TryGetProperty("llmModel", out var modelElement) &&
                modelElement.ValueKind == JsonValueKind.String &&
                string.Equals(modelElement.GetString(), selectedModel, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ChatRole ParseRole(string role) => role.ToLowerInvariant() switch
    {
        "system" => ChatRole.System,
        "assistant" => ChatRole.Assistant,
        _ => ChatRole.User
    };

    private static CapabilityResult Success(
        string requestId,
        BrokerLlmChunk chunk,
        int sequence,
        bool hasMore) => new()
    {
        RequestId = requestId,
        Succeeded = true,
        ContentType = "application/json",
        Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(chunk, JsonOptions)),
        Sequence = sequence,
        HasMore = hasMore
    };

    private static CapabilityResult Failure(string requestId, string error) => new()
    {
        RequestId = requestId,
        Succeeded = false,
        ContentType = "application/json",
        Error = error,
        HasMore = false
    };
}
