using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class AgentSessionRegistryTests
{
    [Fact]
    public async Task PublishEvent_DeliversOnlyToAuthorizedSessionsInSameBusiness()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var source = Register(
            registry,
            "source",
            "business-1",
            publications: ["com.example.changed.v1"]);
        var authorized = Register(
            registry,
            "authorized",
            "business-1",
            subscriptions: ["com.example.changed.v1"]);
        var otherBusiness = Register(
            registry,
            "other-business",
            "business-2",
            subscriptions: ["com.example.changed.v1"]);

        registry.PublishEvent(
            source,
            new PublishEvent
            {
                EventType = "com.example.changed.v1",
                SchemaVersion = "1",
                Subject = "resource/1",
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{}")
            },
            "correlation-1");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var delivered = await authorized.Outbound.ReadAsync(timeout.Token);

        Assert.Equal(
            BrokerToAgentMessage.PayloadOneofCase.Event,
            delivered.PayloadCase);
        Assert.Equal("source", delivered.Event.SourceAgentId);
        Assert.Equal("com.example.changed.v1", delivered.Event.EventType);
        Assert.False(otherBusiness.Outbound.TryRead(out _));
    }

    [Fact]
    public async Task CapabilityResult_ReturnsOnlyFromBrokerSelectedProvider()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "requester", "business-1");
        var provider = Register(
            registry,
            "provider",
            "business-1",
            capabilities: ["example.lookup.v1"]);

        registry.RequestCapability(
            requester,
            new RequestCapability
            {
                RequestId = "request-1",
                Capability = "example.lookup.v1",
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{}")
            },
            "correlation-1");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var request = await provider.Outbound.ReadAsync(timeout.Token);

        Assert.Equal(
            BrokerToAgentMessage.PayloadOneofCase.CapabilityRequest,
            request.PayloadCase);
        Assert.Equal("requester", request.CapabilityRequest.RequestingAgentId);

        registry.CompleteCapability(
            provider,
            new CapabilityResult
            {
                RequestId = "request-1",
                Succeeded = true,
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{\"value\":42}")
            },
            "correlation-1");

        var response = await requester.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(
            BrokerToAgentMessage.PayloadOneofCase.CapabilityResult,
            response.PayloadCase);
        Assert.True(response.CapabilityResult.Succeeded);
    }

    private static AgentSession Register(
        AgentSessionRegistry registry,
        string agentId,
        string businessId,
        IReadOnlySet<string>? capabilities = null,
        IReadOnlySet<string>? subscriptions = null,
        IReadOnlySet<string>? publications = null) =>
        registry.Register(
            new RegisterAgent
            {
                AgentId = agentId,
                AgentVersion = "1.0.0",
                InstallationId = $"{agentId}-installation",
                BusinessId = businessId
            },
            new AuthorizedAgentGrant(
                capabilities ?? new HashSet<string>(StringComparer.Ordinal),
                subscriptions ?? new HashSet<string>(StringComparer.Ordinal),
                publications ?? new HashSet<string>(StringComparer.Ordinal)));
}
