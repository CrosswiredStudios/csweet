using System.Runtime.CompilerServices;
using System.Net.Http;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using Google.Protobuf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CSweet.Agents.PersonalAssistant;

public sealed class PersonalAssistantAgent : CSweetAgentBase
{
    private readonly IAgentLlmClientFactory? _llmClientFactory;
    private readonly ILogger<PersonalAssistantAgent> _logger;

    public PersonalAssistantAgent(ILogger<PersonalAssistantAgent> logger)
    {
        _logger = logger;
    }

    public PersonalAssistantAgent(
        IAgentLlmClientFactory llmClientFactory,
        ILogger<PersonalAssistantAgent> logger,
        IServiceScopeFactory? scopeFactory = null)
    {
        _llmClientFactory = llmClientFactory;
        _logger = logger;
    }

    public override string AgentId => PersonalAssistantProfile.AgentId;

    public override string Version => PersonalAssistantProfile.Version;

    protected override string ConfigurationSchemaVersion => PersonalAssistantProfile.ConfigurationSchemaVersion;

    protected override AgentConfigurationBuilder Configure(AgentConfigurationBuilder builder)
    {
        return builder
            .LlmProvider(
                "llmProviderId",
                "LLM Provider",
                required: true,
                description: "Selects the provider profile the Personal Assistant should use when it is allowed to call a user-configured model.")
            .LlmModel(
                "llmModel",
                "Model",
                dependsOnFieldKey: "llmProviderId",
                required: true,
                description: "Selects the chat model to use from the chosen provider profile.")
            .Select(
                "responseTone",
                "Response Tone",
                [
                    new AgentConfigurationOption("concise", "Concise"),
                    new AgentConfigurationOption("balanced", "Balanced"),
                    new AgentConfigurationOption("detailed", "Detailed")
                ],
                required: true,
                description: "Controls how much detail the assistant uses in executive responses.",
                defaultValue: "balanced")
            .Boolean(
                "proactivePlanning",
                "Proactive Planning",
                required: true,
                description: "Allows the assistant to suggest plans and follow-up work without being explicitly asked.",
                defaultValue: true)
            .Number(
                "maxPlanItems",
                "Maximum Plan Items",
                required: true,
                description: "Caps the number of tasks the assistant proposes in a single plan.",
                minimum: 3,
                maximum: 20,
                step: 1,
                defaultValue: 8)
            .TextArea(
                "customInstructions",
                "Custom Instructions",
                description: "Optional operating guidance that is appended to the assistant's built-in instructions.",
                placeholder: "Example: Prefer short plans with clear owners and approval points.");
    }

    public override async Task HandleEventAsync(
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

        var incoming = DeserializePayload<UserMessageReceived>(message.Payload);

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
        var usage = new UsageDetails();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sequence = 0;

        _logger.LogInformation(
            "Personal Assistant received user message event {EventId} for conversation {ConversationId}. Provider {ProviderProfileId}. MessageLength {MessageLength}.",
            message.EventId,
            conversationId,
            incoming.ProviderProfileId,
            incoming.Message.Length);

        try
        {
            await foreach (var update in StreamAssistantDeltasAsync(
                new AssistantCapabilityInput(
                    incoming.ProviderProfileId,
                    conversationId,
                    incoming.Message,
                    incoming.Context),
                PersonalAssistantProfile.ConverseCapability,
                context,
                cancellationToken))
            {
                if (update.Usage is not null)
                {
                    usage.Add(update.Usage);
                }

                if (string.IsNullOrEmpty(update.Delta))
                {
                    continue;
                }

                builder.Append(update.Delta);

                _logger.LogInformation(
                    "Personal Assistant publishing chunk for conversation {ConversationId}. Sequence {Sequence}. DeltaLength {DeltaLength}.",
                    conversationId,
                    sequence,
                    update.Delta.Length);

                await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
                    conversationId,
                    sequence++,
                    update.Delta,
                    IsFinal: false), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Personal Assistant failed to generate a response for conversation {ConversationId}.",
                conversationId);

            await PublishAgentErrorAsync(
                context,
                message.EventId,
                conversationId,
                sequence,
                BuildSafeFailureMessage(exception),
                cancellationToken);
            await WriteRunLogAsync(
                incoming.ProviderProfileId,
                incoming.Message,
                output: null,
                status: "Failed",
                startedAt,
                stopwatch.ElapsedMilliseconds,
                usage: null,
                exception.Message,
                cancellationToken);
            return;
        }

        if (builder.Length == 0)
        {
            _logger.LogWarning(
                "Personal Assistant generated an empty response for conversation {ConversationId}.",
                conversationId);

            await PublishAgentErrorAsync(
                context,
                message.EventId,
                conversationId,
                sequence,
                "The Personal Assistant could not complete the request because the model provider returned an empty response.",
                cancellationToken);
            await WriteRunLogAsync(
                incoming.ProviderProfileId,
                incoming.Message,
                output: null,
                status: "Failed",
                startedAt,
                stopwatch.ElapsedMilliseconds,
                usage,
                "The model provider returned an empty response.",
                cancellationToken);
            return;
        }

        await PublishChunkAsync(context, message.EventId, new AssistantResponseChunk(
            conversationId, sequence, Delta: string.Empty, IsFinal: true), cancellationToken);

        _logger.LogInformation(
            "Personal Assistant completed streaming for conversation {ConversationId}. Chunks {ChunkCount}. ResponseLength {ResponseLength}.",
            conversationId,
            sequence,
            builder.Length);

        await context.Broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantProfile.AssistantResponseCreatedEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{conversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(SerializePayload(
                    new AssistantResponseCreated(conversationId, builder.ToString(), ProposedActions: [], DateTimeOffset.UtcNow)))
            },
            message.EventId,
            cancellationToken);

        await WriteRunLogAsync(
            incoming.ProviderProfileId,
            incoming.Message,
            builder.ToString(),
            "Completed",
            startedAt,
            stopwatch.ElapsedMilliseconds,
            usage,
            failureMessage: null,
            cancellationToken);
    }

    protected override async Task<AgentCapabilityExecutionResult> ExecuteCapabilityCoreAsync(
        CapabilityRequest request,
        AgentRuntimeContext context,
        CancellationToken cancellationToken)
    {
        if (!IsSupportedCapability(request.Capability))
        {
            return AgentCapabilityExecutionResult.Failure(
                $"Capability '{request.Capability}' is not supported by the Personal Assistant.");
        }

        var input = DeserializePayload<AssistantCapabilityInput>(request.Payload);

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

            return AgentCapabilityExecutionResult.Success(SerializePayload(response));
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
                Payload = ByteString.CopyFrom(SerializePayload(chunk))
            },
            correlationId,
            cancellationToken);
    }

    private static Task PublishAgentErrorAsync(
        AgentRuntimeContext context,
        string correlationId,
        string conversationId,
        int sequence,
        string message,
        CancellationToken cancellationToken)
    {
        return PublishChunkAsync(context, correlationId, new AssistantResponseChunk(
            conversationId,
            sequence,
            message,
            IsFinal: true,
            Error: "agent_error"), cancellationToken);
    }

    private static string BuildSafeFailureMessage(Exception exception)
    {
        var candidates = exception is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions
            : [exception];

        var httpException = candidates
            .SelectMany(EnumerateExceptionChain)
            .OfType<HttpRequestException>()
            .FirstOrDefault();

        if (httpException is not null)
        {
            return $"The model provider could not be reached: {httpException.Message}";
        }

        return "The Personal Assistant could not complete the request. Check the Personal Assistant logs for details.";
    }

    private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    private async IAsyncEnumerable<AssistantStreamUpdate> StreamAssistantDeltasAsync(
        AssistantCapabilityInput input,
        string capability,
        AgentRuntimeContext runtimeContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Personal Assistant resolving chat client for provider {ProviderProfileId} and conversation {ConversationId}.",
            input.ProviderProfileId,
            input.ConversationId);

        var selection = new AgentLlmSelection(
            input.ProviderProfileId,
            Settings.GetString("llmModel"));
        var chatClient = _llmClientFactory is null
            ? new BrokerLlmClient(runtimeContext.Broker, selection)
            : await _llmClientFactory.CreateChatClientAsync(selection, cancellationToken);

        _logger.LogInformation(
            "Personal Assistant created chat client for provider {ProviderProfileId} and conversation {ConversationId}.",
            input.ProviderProfileId,
            input.ConversationId);

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

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);

        _logger.LogInformation(
            "Personal Assistant starting MAF streaming for conversation {ConversationId}. Capability {Capability}. PromptLength {PromptLength}.",
            input.ConversationId,
            capability,
            prompt.Length);

        await foreach (var update in agent.RunStreamingAsync(prompt, session, options: null, cancellationToken))
        {
            var usage = ExtractUsage(update.Contents);
            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new AssistantStreamUpdate(update.Text, usage);
            }
            else if (usage is not null)
            {
                yield return new AssistantStreamUpdate(string.Empty, usage);
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

        await foreach (var update in StreamAssistantDeltasAsync(input, capability, runtimeContext, cancellationToken))
        {
            builder.Append(update.Delta);
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

    private static Task WriteRunLogAsync(
        Guid providerProfileId,
        string prompt,
        string? output,
        string status,
        DateTimeOffset startedAt,
        long durationMs,
        UsageDetails? usage,
        string? failureMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    private static UsageDetails? ExtractUsage(IEnumerable<AIContent> contents)
    {
        UsageDetails? usage = null;

        foreach (var usageContent in contents.OfType<UsageContent>())
        {
            usage ??= new UsageDetails();
            usage.Add(usageContent.Details);
        }

        return usage;
    }

    private sealed record AssistantStreamUpdate(string Delta, UsageDetails? Usage);
}
