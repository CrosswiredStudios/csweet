using System.Text;
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using Google.Protobuf;
using Microsoft.AspNetCore.Http.Features;

namespace CSweet.Api.Chat;

public static class ChatMessageEndpoints
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan AgentResponseTimeout = TimeSpan.FromSeconds(90);

    public static IEndpointRouteBuilder MapChatMessageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
            "/api/core/organizations/{organizationId:guid}/conversations/{conversationId:guid}/messages/stream",
            StreamAsync);

        return endpoints;
    }

    private static async Task StreamAsync(
        Guid organizationId,
        Guid conversationId,
        SendChatMessageRequest request,
        HttpContext http,
        IConversationService conversations,
        IAgentBrokerClient broker,
        IAgentInteractiveRuntimeService interactiveRuntime,
        IAgentInstallationConfigurationService configurations,
        IChatStreamRouter router,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("CSweet.Api.Chat.ChatMessageEndpoints");

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            logger.LogWarning(
                "Rejected empty chat message for conversation {ConversationId} in organization {OrganizationId}.",
                conversationId,
                organizationId);

            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "Message is required." }, cancellationToken);
            return;
        }

        var conversation = await conversations.GetAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.OrganizationId != organizationId)
        {
            logger.LogWarning(
                "Rejected chat message for missing or mismatched conversation {ConversationId} in organization {OrganizationId}.",
                conversationId,
                organizationId);

            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var agentInstallationId = await conversations.GetAgentInstallationIdAsync(conversationId, cancellationToken);
        if (agentInstallationId is null)
        {
            logger.LogWarning(
                "Rejected chat message for conversation {ConversationId}: the agent employee has no installation.",
                conversationId);
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(
                new { error = "This agent employee is not linked to an imported agent installation." },
                cancellationToken);
            return;
        }

        var persistedConfiguration = await configurations.GetAsync(
            agentInstallationId.Value,
            cancellationToken);
        var configuredProviderId = GetConfiguredProviderId(persistedConfiguration);
        var providerId = configuredProviderId.HasValue &&
            await conversations.IsProviderProfileEnabledAsync(configuredProviderId.Value, cancellationToken)
                ? configuredProviderId
                : await conversations.GetDefaultProviderProfileIdAsync(cancellationToken);
        if (providerId is null)
        {
            logger.LogWarning(
                "Rejected chat message for conversation {ConversationId}: no enabled LLM provider is configured.",
                conversationId);

            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(
                new { error = "No enabled LLM provider is configured. Finish setup first." },
                cancellationToken);
            return;
        }

        AgentRuntimeReadinessResponse readiness;
        try
        {
            readiness = await interactiveRuntime.EnsureReadyAsync(agentInstallationId.Value, cancellationToken);
        }
        catch (AgentInstallationException exception)
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(new { error = exception.Message }, cancellationToken);
            return;
        }

        if (!readiness.IsReady)
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(
                new { error = "The agent runtime is still starting.", runtime = readiness },
                cancellationToken);
            return;
        }

        if (persistedConfiguration is not null)
        {
            var hydration = await InvokeConfigurationUpdateAsync(
                broker,
                agentInstallationId.Value,
                persistedConfiguration,
                cancellationToken);
            if (!hydration.Succeeded)
            {
                http.Response.StatusCode = StatusCodes.Status409Conflict;
                await http.Response.WriteAsJsonAsync(
                    new { error = hydration.Error ?? "The saved agent configuration could not be applied." },
                    cancellationToken);
                return;
            }
        }

        logger.LogInformation(
            "Chat stream starting for conversation {ConversationId}, organization {OrganizationId}, agent user {AgentOrganizationUserId}, provider {ProviderProfileId}.",
            conversationId,
            organizationId,
            conversation.AgentOrganizationUserId,
            providerId.Value);

        await conversations.AppendMessageAsync(
            conversationId,
            ConversationRole.User,
            request.Message,
            cancellationToken);

        logger.LogInformation(
            "Persisted user chat message and subscribed to assistant chunks for conversation {ConversationId}.",
            conversationId);

        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache, no-transform";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await http.Response.StartAsync(cancellationToken);

        var reader = router.Subscribe(conversationId);

        var payload = new UserMessageReceived(
            providerId.Value,
            conversationId.ToString(),
            conversation.InitiatedByOrganizationUserId.ToString(),
            request.Message,
            Context: null);

        // TODO(targeting): user.message.received is broadcast to same-business subscribers.
        // Store or route by the concrete target agent id before adding a second chat-capable agent.
        logger.LogInformation(
            "Publishing user message event {EventType} for conversation {ConversationId} with correlation {CorrelationId}.",
            AgentChatEvents.UserMessageReceivedEvent,
            conversationId,
            conversationId);

        await broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = AgentChatEvents.UserMessageReceivedEvent,
                SchemaVersion = "1",
                Subject = $"agent-installation/{agentInstallationId}/conversation/{conversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions))
            },
            conversationId.ToString(),
            cancellationToken);

        var assembled = new StringBuilder();
        var completed = false;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(AgentResponseTimeout);

        try
        {
            await foreach (var chunk in reader.ReadAllAsync(timeoutCts.Token))
            {
                if (chunk.Error is not null)
                {
                    logger.LogWarning(
                        "Received assistant error chunk for conversation {ConversationId}. Sequence {Sequence}. Error {Error}. Message {Message}",
                        conversationId,
                        chunk.Sequence,
                        chunk.Error,
                        chunk.Delta);
                }
                else
                {
                    logger.LogInformation(
                        "Received assistant chunk for conversation {ConversationId}. Sequence {Sequence}. IsFinal {IsFinal}. DeltaLength {DeltaLength}.",
                        conversationId,
                        chunk.Sequence,
                        chunk.IsFinal,
                        chunk.Delta.Length);
                }

                if (chunk.Error is null && !chunk.IsFinal && chunk.Delta.Length > 0)
                {
                    assembled.Append(chunk.Delta);
                }

                await WriteSseChunkAsync(http, chunk, cancellationToken);

                if (chunk.IsFinal)
                {
                    completed = chunk.Error is null;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "Timed out waiting for assistant response for conversation {ConversationId} after {TimeoutSeconds} seconds.",
                conversationId,
                AgentResponseTimeout.TotalSeconds);

            await WriteSseChunkAsync(
                http,
                new ChatStreamChunk(
                    -1,
                    "The assistant did not respond in time.",
                    IsFinal: true,
                    Error: "timeout"),
                CancellationToken.None);
        }
        finally
        {
            router.Complete(conversationId);
            logger.LogInformation(
                "Completed chat stream router subscription for conversation {ConversationId}.",
                conversationId);
        }

        if (completed && assembled.Length > 0)
        {
            await conversations.AppendMessageAsync(
                conversationId,
                ConversationRole.Assistant,
                assembled.ToString(),
                CancellationToken.None);

            logger.LogInformation(
                "Persisted assistant response for conversation {ConversationId}. ResponseLength {ResponseLength}.",
                conversationId,
                assembled.Length);
        }
        else if (!completed)
        {
            logger.LogWarning(
                "Chat stream ended without a successful assistant response for conversation {ConversationId}. ResponseLength {ResponseLength}.",
                conversationId,
                assembled.Length);
        }
    }

    private static async Task WriteSseChunkAsync(
        HttpContext http,
        ChatStreamChunk chunk,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                sequence = chunk.Sequence,
                delta = chunk.Delta,
                isFinal = chunk.IsFinal,
                error = chunk.Error
            },
            SerializerOptions);

        await http.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }

    private static Guid? GetConfiguredProviderId(
        AgentInstallationConfigurationSnapshot? configuration)
    {
        if (configuration?.Settings.TryGetValue("llmProviderId", out var value) != true ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return Guid.TryParse(value.GetString(), out var providerId) ? providerId : null;
    }

    private static async Task<CapabilityResult> InvokeConfigurationUpdateAsync(
        IAgentBrokerClient broker,
        Guid installationId,
        AgentInstallationConfigurationSnapshot configuration,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var request = new CSweet.Contracts.Agents.UpdateAgentConfigurationRequest(configuration.Settings)
        {
            SchemaVersion = configuration.SchemaVersion
        };
        return await broker.InvokeCapabilityAsync(
            new RequestCapability
            {
                Capability = CSweet.Contracts.Agents.AgentConfigurationCapabilities.Update,
                TargetAgentId = $"installation:{installationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions))
            },
            Guid.NewGuid().ToString("N"),
            timeout.Token);
    }
}
