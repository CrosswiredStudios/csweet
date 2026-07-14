using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Application.Llm;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Agents.PersonalAssistant;
using CSweet.Domain.Setup;
using Google.Protobuf;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class PersonalAssistantAgentTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HandleEventAsync_StreamsChunksThenFinalResponse()
    {
        var deltas = new[] { "Here is ", "today's ", "summary." };
        var broker = await RunAgentAsync(new StreamingChatClient(deltas));

        var chunkEvents = broker.PublishedEvents
            .Where(e => e.EventType == PersonalAssistantProfile.AssistantResponseChunkEvent)
            .ToList();

        var chunks = chunkEvents
            .Select(e => JsonSerializer.Deserialize<AssistantResponseChunk>(
                e.Payload.ToByteArray(), SerializerOptions)!)
            .ToList();

        // Three content chunks with increasing sequence, then a terminal chunk.
        Assert.Equal(deltas.Length + 1, chunks.Count);
        for (var i = 0; i < deltas.Length; i++)
        {
            Assert.Equal(i, chunks[i].Sequence);
            Assert.Equal(deltas[i], chunks[i].Delta);
            Assert.False(chunks[i].IsFinal);
        }

        var terminal = chunks[^1];
        Assert.True(terminal.IsFinal);
        Assert.Equal(deltas.Length, terminal.Sequence);
        Assert.Equal(string.Empty, terminal.Delta);

        // Every chunk uses the conversation subject.
        Assert.All(chunkEvents, e => Assert.Equal("conversation/conversation-1", e.Subject));

        // Final response carries the concatenation of all deltas.
        var final = Assert.Single(
            broker.PublishedEvents,
            e => e.EventType == PersonalAssistantProfile.AssistantResponseCreatedEvent);
        var response = JsonSerializer.Deserialize<AssistantResponseCreated>(
            final.Payload.ToByteArray(), SerializerOptions);
        Assert.NotNull(response);
        Assert.Equal("conversation-1", response.ConversationId);
        Assert.Equal(string.Concat(deltas), response.Response);
        Assert.Empty(response.ProposedActions);
    }

    [Fact]
    public async Task HandleEventAsync_MalformedMessage_PublishesNothing()
    {
        var agent = new PersonalAssistantAgent(
            new FakeAgentLlmClientFactory(new StreamingChatClient(["ignored"])),
            NullLogger<PersonalAssistantAgent>.Instance);
        var broker = new RecordingBrokerClient();
        var context = new AgentRuntimeContext("business-1", "assistant-installation", broker);

        var malformed = new UserMessageReceived(
            Guid.Empty,
            "conversation-1",
            "user-1",
            string.Empty,
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
                    JsonSerializer.SerializeToUtf8Bytes(malformed, SerializerOptions))
            },
            context,
            CancellationToken.None);

        Assert.Empty(broker.PublishedEvents);
    }

    [Fact]
    public async Task HandleEventAsync_EmptyModelStream_PublishesErrorChunk()
    {
        var broker = await RunAgentAsync(new StreamingChatClient([]));

        var chunkEvent = Assert.Single(
            broker.PublishedEvents,
            e => e.EventType == PersonalAssistantProfile.AssistantResponseChunkEvent);

        var chunk = JsonSerializer.Deserialize<AssistantResponseChunk>(
            chunkEvent.Payload.ToByteArray(), SerializerOptions);

        Assert.NotNull(chunk);
        Assert.True(chunk.IsFinal);
        Assert.Equal("agent_error", chunk.Error);
        Assert.Contains("could not complete", chunk.Delta);
        Assert.DoesNotContain(
            broker.PublishedEvents,
            e => e.EventType == PersonalAssistantProfile.AssistantResponseCreatedEvent);
    }

    [Fact]
    public async Task HandleEventAsync_StreamingUsage_DoesNotRequirePlatformPersistence()
    {
        var providerProfileId = Guid.NewGuid();
        var agent = new PersonalAssistantAgent(
            new FakeAgentLlmClientFactory(new UsageStreamingChatClient()),
            NullLogger<PersonalAssistantAgent>.Instance);
        var broker = new RecordingBrokerClient();
        var context = new AgentRuntimeContext("business-1", "assistant-installation", broker);

        await agent.HandleEventAsync(
            BuildUserMessage(providerProfileId),
            context,
            CancellationToken.None);

        Assert.Contains(
            broker.PublishedEvents,
            e => e.EventType == PersonalAssistantProfile.AssistantResponseCreatedEvent);
    }

    private static async Task<RecordingBrokerClient> RunAgentAsync(IChatClient chatClient)
    {
        var agent = new PersonalAssistantAgent(
            new FakeAgentLlmClientFactory(chatClient),
            NullLogger<PersonalAssistantAgent>.Instance);
        var broker = new RecordingBrokerClient();
        var context = new AgentRuntimeContext("business-1", "assistant-installation", broker);

        var incoming = new UserMessageReceived(
            Guid.NewGuid(),
            "conversation-1",
            "user-1",
            "What happened today?",
            new Dictionary<string, string>());

        await agent.HandleEventAsync(
            BuildUserMessage(incoming.ProviderProfileId),
            context,
            CancellationToken.None);

        return broker;
    }

    private static DeliveredEvent BuildUserMessage(Guid providerProfileId)
    {
        var incoming = new UserMessageReceived(
            providerProfileId,
            "conversation-1",
            "user-1",
            "What happened today?",
            new Dictionary<string, string>());

        return new DeliveredEvent
        {
            EventId = "event-1",
            EventType = PersonalAssistantProfile.UserMessageReceivedEvent,
            SchemaVersion = "1",
            Subject = "conversation/conversation-1",
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(
                JsonSerializer.SerializeToUtf8Bytes(incoming, SerializerOptions))
        };
    }

    private sealed class FakeAgentLlmClientFactory(IChatClient chatClient) : IAgentLlmClientFactory
    {
        public Task<IChatClient> CreateChatClientAsync(
            AgentLlmSelection selection,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(chatClient);
    }

    private sealed class StreamingChatClient(IReadOnlyList<string> deltas) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, string.Concat(deltas))));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var delta in deltas)
            {
                await Task.Yield();
                yield return new ChatResponseUpdate(ChatRole.Assistant, delta);
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class UsageStreamingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "usage response")));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "usage response");
            yield return new ChatResponseUpdate(ChatRole.Assistant, [
                new UsageContent(new UsageDetails
                {
                    InputTokenCount = 42,
                    OutputTokenCount = 17
                })
            ]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingAgentRunLogWriter(List<AgentRunLog> logs) : IAgentRunLogWriter
    {
        public Task WriteAsync(AgentRunLog log, CancellationToken cancellationToken = default)
        {
            logs.Add(log);
            return Task.CompletedTask;
        }
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
