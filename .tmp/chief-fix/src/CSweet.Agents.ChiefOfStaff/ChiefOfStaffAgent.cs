using System.Runtime.CompilerServices;
using System.Net.Http;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using Google.Protobuf;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CSweet.Memory;

namespace CSweet.Agents.ChiefOfStaff;

public sealed class ChiefOfStaffAgent : CSweetAgentBase
{
    private readonly IAgentLlmClientFactory? _llmClientFactory;
    private readonly ILogger<ChiefOfStaffAgent> _logger;

    public ChiefOfStaffAgent(ILogger<ChiefOfStaffAgent> logger)
    {
        _logger = logger;
    }

    public ChiefOfStaffAgent(
        IAgentLlmClientFactory llmClientFactory,
        ILogger<ChiefOfStaffAgent> logger)
    {
        _llmClientFactory = llmClientFactory;
        _logger = logger;
    }

    public override string AgentId => ChiefOfStaffProfile.AgentId;

    public override string Version => ChiefOfStaffProfile.Version;

    protected override string ConfigurationSchemaVersion => ChiefOfStaffProfile.ConfigurationSchemaVersion;

    protected override AgentConfigurationBuilder Configure(AgentConfigurationBuilder builder)
    {
        return builder
            .LlmProvider(
                "llmProviderId",
                "LLM Provider",
                required: true,
                description: "Selects the provider profile the Chief of Staff should use when it is allowed to call a user-configured model.")
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
                ChiefOfStaffProfile.UserMessageReceivedEvent,
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
            "Chief of Staff received user message event {EventId} for conversation {ConversationId}. Provider {ProviderProfileId}. MessageLength {MessageLength}.",
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
                    incoming.Context,
                    incoming.UserId),
                ChiefOfStaffProfile.ConverseCapability,
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
                    "Chief of Staff publishing chunk for conversation {ConversationId}. Sequence {Sequence}. DeltaLength {DeltaLength}.",
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
                "Chief of Staff failed to generate a response for conversation {ConversationId}.",
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
                "Chief of Staff generated an empty response for conversation {ConversationId}.",
                conversationId);

            await PublishAgentErrorAsync(
                context,
                message.EventId,
                conversationId,
                sequence,
                "The Chief of Staff could not complete the request because the model provider returned an empty response.",
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
            "Chief of Staff completed streaming for conversation {ConversationId}. Chunks {ChunkCount}. ResponseLength {ResponseLength}.",
            conversationId,
            sequence,
            builder.Length);

        await context.Broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = ChiefOfStaffProfile.AssistantResponseCreatedEvent,
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
                $"Capability '{request.Capability}' is not supported by the Chief of Staff.");
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
                "Chief of Staff failed capability {Capability}.",
                request.Capability);

            return AgentCapabilityExecutionResult.Failure(
                "The Chief of Staff could not complete the request.");
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
                EventType = ChiefOfStaffProfile.AssistantResponseChunkEvent,
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

        return "The Chief of Staff could not complete the request. Check the Chief of Staff logs for details.";
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
            "Chief of Staff resolving chat client for provider {ProviderProfileId} and conversation {ConversationId}.",
            input.ProviderProfileId,
            input.ConversationId);

        var selection = new AgentLlmSelection(
            input.ProviderProfileId,
            Settings.GetString("llmModel"));
        var chatClient = _llmClientFactory is null
            ? new BrokerLlmClient(runtimeContext.Broker, selection)
            : await _llmClientFactory.CreateChatClientAsync(selection, cancellationToken);

        _logger.LogInformation(
            "Chief of Staff created chat client for provider {ProviderProfileId} and conversation {ConversationId}.",
            input.ProviderProfileId,
            input.ConversationId);

        var memoryOptions = Options.Create(new AgentMemoryOptions
        {
            DefaultScope = MemoryScope.User,
            ContextTokenBudget = 2_000,
            StoreAssistantMessages = true,
            FailOpen = true
        });
        var memoryStore = new CSweetBrokerMemoryStore(runtimeContext.Broker);
        var memoryEngine = new MemoryEngine(
            memoryStore,
            memoryOptions,
            authorizer: new DelegatedMemoryScopeAuthorizer(),
            namespaceResolver: new WorkContextMemoryNamespaceResolver());
        var memoryProvider = new AgentMemoryContextProvider(
            memoryEngine,
            new SessionStateMemoryPartitionResolver(memoryOptions),
            memoryOptions);

        AIAgent agent = new ChatClientAgent(
            chatClient,
            new ChatClientAgentOptions
            {
                Id = ChiefOfStaffProfile.AgentId,
                Name = "C-Sweet Chief of Staff",
                ChatOptions = new ChatOptions { Instructions = ChiefOfStaffProfile.SystemPrompt },
                AIContextProviders = [memoryProvider]
            });

        var prompt = capability switch
        {
            ChiefOfStaffProfile.SummarizeActivityCapability =>
                $"Summarize the relevant company activity for the executive.\n\n{input.Prompt}",
            ChiefOfStaffProfile.PlanWorkCapability =>
                $"Create a practical work plan. Identify required capabilities, risks, approvals, and next steps.\n\n{input.Prompt}",
            _ => input.Prompt
        };

        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        session.ConfigureMemory(
            new MemoryPartition(
                runtimeContext.BusinessId,
                runtimeContext.InstallationId,
                ChiefOfStaffProfile.AgentId,
                input.UserId ?? ResolveUserId(input.Context),
                input.ConversationId),
            MemoryScope.User,
            new MemoryPrincipal(
                runtimeContext.BusinessId,
                ChiefOfStaffProfile.AgentId,
                ChiefOfStaffProfile.AgentId,
                runtimeContext.InstallationId,
                Attributes: new Dictionary<string, string>
                {
                    ["memory.maxSensitivity"] = MemorySensitivity.Personal.ToString()
                }));

        _logger.LogInformation(
            "Chief of Staff starting MAF streaming for conversation {ConversationId}. Capability {Capability}. PromptLength {PromptLength}.",
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

    private static string? ResolveUserId(IReadOnlyDictionary<string, string>? context) =>
        context is not null && context.TryGetValue("userId", out var userId) && !string.IsNullOrWhiteSpace(userId)
            ? userId
            : null;

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
        capability is ChiefOfStaffProfile.ConverseCapability or
            ChiefOfStaffProfile.SummarizeActivityCapability or
            ChiefOfStaffProfile.PlanWorkCapability;

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
