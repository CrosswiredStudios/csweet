using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Application.Llm;
using CSweet.Contracts.Llm;
using Google.Protobuf;
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

        var response = await GenerateResponseAsync(
            new AssistantCapabilityInput(
                incoming.ProviderProfileId,
                incoming.ConversationId,
                incoming.Message,
                incoming.Context),
            PersonalAssistantProfile.ConverseCapability,
            context,
            cancellationToken);

        await context.Broker.PublishEventAsync(
            new PublishEvent
            {
                EventType = PersonalAssistantProfile.AssistantResponseCreatedEvent,
                SchemaVersion = "1",
                Subject = $"conversation/{incoming.ConversationId}",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(
                    JsonSerializer.SerializeToUtf8Bytes(response, SerializerOptions))
            },
            message.EventId,
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

    private async Task<AssistantResponseCreated> GenerateResponseAsync(
        AssistantCapabilityInput input,
        string capability,
        AgentRuntimeContext runtimeContext,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IAgentRunner>();

        var prompt = capability switch
        {
            PersonalAssistantProfile.SummarizeActivityCapability =>
                $"Summarize the relevant company activity for the executive.\n\n{input.Prompt}",
            PersonalAssistantProfile.PlanWorkCapability =>
                $"Create a practical work plan. Identify required capabilities, risks, approvals, and next steps.\n\n{input.Prompt}",
            _ => input.Prompt
        };

        var assembledContext = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["businessId"] = runtimeContext.BusinessId,
            ["conversationId"] = input.ConversationId,
            ["requestedCapability"] = capability
        };

        if (input.Context is not null)
        {
            foreach (var item in input.Context)
            {
                assembledContext[item.Key] = item.Value;
            }
        }

        var result = await runner.RunAsync(
            new AgentRunRequest(
                input.ProviderProfileId,
                PersonalAssistantProfile.AgentKey,
                PersonalAssistantProfile.SystemPrompt,
                prompt,
                assembledContext,
                new AgentRunOptions(
                    Temperature: 0.2,
                    MaxOutputTokens: 2048,
                    RequireStructuredOutput: false,
                    OutputSchemaJson: null)),
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                result.FailureMessage ?? "The configured model provider failed.");
        }

        return new AssistantResponseCreated(
            input.ConversationId,
            result.Content ?? string.Empty,
            ProposedActions: [],
            DateTimeOffset.UtcNow);
    }

    private static bool IsSupportedCapability(string capability) =>
        capability is PersonalAssistantProfile.ConverseCapability or
            PersonalAssistantProfile.SummarizeActivityCapability or
            PersonalAssistantProfile.PlanWorkCapability;
}
