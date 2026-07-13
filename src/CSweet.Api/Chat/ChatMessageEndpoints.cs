using System.Text;
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Application.Core;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using Google.Protobuf;

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
        IChatStreamRouter router,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            http.Response.StatusCode = StatusCodes.Status400BadRequest;
            await http.Response.WriteAsJsonAsync(new { error = "Message is required." }, cancellationToken);
            return;
        }

        var conversation = await conversations.GetAsync(conversationId, cancellationToken);
        if (conversation is null || conversation.OrganizationId != organizationId)
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var providerId = await conversations.GetDefaultProviderProfileIdAsync(cancellationToken);
        if (providerId is null)
        {
            http.Response.StatusCode = StatusCodes.Status409Conflict;
            await http.Response.WriteAsJsonAsync(
                new { error = "No enabled LLM provider is configured. Finish setup first." },
                cancellationToken);
            return;
        }

        await conversations.AppendMessageAsync(
            conversationId,
            ConversationRole.User,
            request.Message,
            cancellationToken);

        var reader = router.Subscribe(conversationId);

        var payload = new UserMessageReceived(
            providerId.Value,
            conversationId.ToString(),
            conversation.InitiatedByOrganizationUserId.ToString(),
            request.Message,
            Context: null);

        // TODO(targeting): user.message.received is broadcast to same-business subscribers.
        // Store or route by the concrete target agent id before adding a second chat-capable agent.
        await broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantChatEvents.UserMessageReceivedEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{conversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions))
            },
            conversationId.ToString(),
            cancellationToken);

        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";

        var assembled = new StringBuilder();
        var completed = false;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(AgentResponseTimeout);

        try
        {
            await foreach (var chunk in reader.ReadAllAsync(timeoutCts.Token))
            {
                if (!chunk.IsFinal && chunk.Delta.Length > 0)
                {
                    assembled.Append(chunk.Delta);
                }

                await WriteSseChunkAsync(http, chunk, cancellationToken);

                if (chunk.IsFinal)
                {
                    completed = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await WriteSseChunkAsync(
                http,
                new ChatStreamChunk(-1, "The assistant did not respond in time.", IsFinal: true),
                CancellationToken.None,
                "timeout");
        }
        finally
        {
            router.Complete(conversationId);
        }

        if (completed && assembled.Length > 0)
        {
            await conversations.AppendMessageAsync(
                conversationId,
                ConversationRole.Assistant,
                assembled.ToString(),
                CancellationToken.None);
        }
    }

    private static async Task WriteSseChunkAsync(
        HttpContext http,
        ChatStreamChunk chunk,
        CancellationToken cancellationToken,
        string? error = null)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                sequence = chunk.Sequence,
                delta = chunk.Delta,
                isFinal = chunk.IsFinal,
                error
            },
            SerializerOptions);

        await http.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await http.Response.Body.FlushAsync(cancellationToken);
    }
}
