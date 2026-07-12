using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant;
using CSweet.Application.Llm;
using CSweet.Contracts.Llm;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class PersonalAssistantAgentTests
{
    [Fact]
    public async Task HandleEventAsync_PublishesAssistantResponse()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentRunner>(new StubAgentRunner("Here is today's summary."));
        await using var provider = services.BuildServiceProvider();

        var agent = new PersonalAssistantAgent(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PersonalAssistantAgent>.Instance);
        var broker = new RecordingBrokerClient();
        var context = new AgentRuntimeContext(
            "business-1",
            "assistant-installation",
            broker);
        var incoming = new UserMessageReceived(
            Guid.NewGuid(),
            "conversation-1",
            "user-1",
            "What happened today?",
            new Dictionary<string, string>());

        await agent.HandleEventAsync(
            new DeliveredEvent
            {
                EventId = "event-1",
                EventType = PersonalAssistantProfile.UserMessageReceivedEvent,
                SchemaVersion = "1",
                Subject = "conversation/conversation-1",
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(
                    JsonSerializer.SerializeToUtf8Bytes(incoming, new JsonSerializerOptions(JsonSerializerDefaults.Web)))
            },
            context,
            CancellationToken.None);

        var published = Assert.Single(broker.PublishedEvents);
        Assert.Equal(
            PersonalAssistantProfile.AssistantResponseCreatedEvent,
            published.EventType);

        var response = JsonSerializer.Deserialize<AssistantResponseCreated>(
            published.Payload.ToByteArray(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(response);
        Assert.Equal("conversation-1", response.ConversationId);
        Assert.Equal("Here is today's summary.", response.Response);
        Assert.Empty(response.ProposedActions);
    }

    private sealed class StubAgentRunner(string content) : IAgentRunner
    {
        public Task<AgentRunResult> RunAsync(
            AgentRunRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AgentRunResult(
                Succeeded: true,
                Content: content,
                StructuredJson: null,
                FailureMessage: null,
                Logs: []));
    }

    private sealed class RecordingBrokerClient : IAgentBrokerClient
    {
        public List<PublishEvent> PublishedEvents { get; } = [];

        public Task StartAsync(
            RegisterAgent registration,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task PublishEventAsync(
            PublishEvent message,
            string? correlationId = null,
            CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(message);
            return Task.CompletedTask;
        }

        public Task<CapabilityResult> InvokeCapabilityAsync(
            RequestCapability request,
            string? correlationId = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SendCapabilityResultAsync(
            CapabilityResult result,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
