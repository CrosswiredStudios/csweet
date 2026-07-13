using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AI.Providers;
using Google.Protobuf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSweet.Agents.PersonalAssistant;

public sealed class PersonalAssistantAgent : ICSweetAgent
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PersonalAssistantAgent> _logger;

    public PersonalAssistantAgent(
        IServiceScopeFactory scopeFactory,
        ILogger<PersonalAssistantAgent> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string AgentId => PersonalAssistantProfile.AgentId;

    public string Version => PersonalAssistantProfile.Version;

    public async Task HandleEventAsync(
        DeliveredEvent message,
        AgentRuntimeContext context,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                message.EventType,
                PersonalAssistantProfile.UserMessageReceivedEvent,
                StringComparison.Ordinal))
        {
            return;
        }

        var incoming = JsonSerializer.Deserialize<UserMessageReceived>(
            message.Payload.ToByteArray(),
            SerializerOptions);

        if (incoming is null ||
            incoming.ProviderProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(incoming.Message))
        {
            _logger.LogWarning(
                "Ignored malformed user message event {EventId}.",
                message.EventId);
            return;
        }

        var conversationId = incoming.ConversationId;
        var builder = new System.Text.StringBuilder();
        var sequence = 0;

        await foreach (var delta in StreamAssistantDeltasAsync(
            new AssistantCapabilityInput(
                incoming.ProviderProfileId,
                conversationId,
                incoming.Message,
                incoming.Context),
            PersonalAssistantProfile.ConverseCapability,
            context,
            cancellationToken))
        {
            builder.Append(delta);

            await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
                conversationId,
                sequence++,
                delta,
                IsFinal: false), cancellationToken);
        }

        // Terminal chunk so the gateway knows the stream is complete.
        await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
            conversationId, sequence, Delta: string.Empty, IsFinal: true), cancellationToken);

        // Keep the existing final "response created" event for anything that consumes the whole reply.
        await context.Broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantProfile.AssistantResponseCreatedEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{conversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(
                    new AssistantResponseCreated(conversationId, builder.ToString(), ProposedActions: [], DateTimeOffset.UtcNow),
                    SerializerOptions))
            },
            message.EventId,
            cancellationToken);
    }

    private static Task PublishChunkAsync(
        AgentRuntimeContext context,
        string correlationId,
        AssistantResponseChunk chunk,
        CancellationToken cancellationToken)
    {
        return context.Broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantProfile.AssistantResponseChunkEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{chunk.ConversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(chunk, SerializerOptions))
            },
            correlationId,
            cancellationToken);
    }

    public async Task<AgentCapabilityExecutionResult> ExecuteCapabilityAsync(
        CapabilityRequest request,
        AgentRuntimeContext context,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCapability(request.Capability))
        {
            return AgentCapabilityExecutionResult.Failure(
                $"Capability '{request.Capability}' is not supported by the Personal Assistant.");
        }

        var input = JsonSerializer.Deserialize<AssistantCapabilityInput>(
            request.Payload.ToByteArray(),
            SerializerOptions);

        if (input is null ||
            input.ProviderProfileId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.Prompt))
        {
            return AgentCapabilityExecutionResult.Failure(
                "The capability input is missing a provider profile or prompt.");
        }

        try
        {
            var response = await GenerateResponseAsync(
                input,
                request.Capability,
                context,
                cancellationToken);

            return AgentCapabilityExecutionResult.Success(
                JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Personal Assistant failed capability {Capability}.",
                request.Capability);

            return AgentCapabilityExecutionResult.Failure(
                "The Personal Assistant could not complete the request.");
        }
    }

    private async IAsyncEnumerable<string> StreamAssistantDeltasAsync(
        AssistantCapabilityInput input,
        string capability,
        AgentRuntimeContext runtimeContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Thin seam: reuse the platform's provider factory to get an IChatClient.
        var providerFactory = scope.ServiceProvider.GetRequiredService<ILlmProviderFactory>();
        var chatClient = await providerFactory.CreateChatClientAsync(input.ProviderProfileId, cancellationToken);

        // Build the MAF agent inside the plugin. The system prompt and behavior are ours.
        AIAgent agent = new ChatClientAgent(
            chatClient,
            instructions: PersonalAssistantProfile.SystemPrompt);

        var prompt = capability switch
        {
            PersonalAssistantProfile.SummarizeActivityCapability =>
                $"Summarize the relevant company activity for the executive.\n\n{input.Prompt}",
            PersonalAssistantProfile.PlanWorkCapability =>
                $"Create a practical work plan. Identify required capabilities, risks, approvals, and next steps.\n\n{input.Prompt}",
            _ => input.Prompt
        };

        // Use AgentSession for conversation state management (official MAF pattern).
        // For single-turn streaming this session is ephemeral.
        // Future: serialize/deserialize sessions for cross-request conversation continuity.
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);

        await foreach (var update in agent.RunStreamingAsync(prompt, session, options: null, cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }

    private async Task<AssistantResponseCreated> GenerateResponseAsync(
        AssistantCapabilityInput input,
        string capability,
        AgentRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        var builder = new System.Text.StringBuilder();

        await foreach (var delta in StreamAssistantDeltasAsync(input, capability, runtimeContext, cancellationToken))
        {
            builder.Append(delta);
        }

        return new AssistantResponseCreated(
            input.ConversationId,
            builder.ToString(),
            ProposedActions: [],
            DateTimeOffset.UtcNow);
    }

    private static bool IsSupportedCapability(string capability) =>
        capability is PersonalAssistantProfile.ConverseCapability or
            PersonalAssistantProfile.SummarizeActivityCapability or
            PersonalAssistantProfile.PlanWorkCapability;
}
