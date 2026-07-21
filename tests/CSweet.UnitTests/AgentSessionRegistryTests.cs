using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class AgentSessionRegistryTests
{
    [Fact]
    public void McpCredential_IsDedicatedSessionBoundAndReturnedOnlyOnce()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var session = Register(registry, "agent", "business-1");

        var mcpToken = session.ConsumeInitialMcpAccessToken();

        Assert.Same(session, registry.FindByMcpAccessToken(mcpToken));
        Assert.Null(registry.FindByMcpAccessToken("forged-token"));
        Assert.Throws<InvalidOperationException>(() => session.ConsumeInitialMcpAccessToken());
    }

    [Fact]
    public async Task PublishEvent_DeliversOnlyToAuthorizedSessionsInSameBusiness()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var source = Register(
            registry,
            "source",
            "business-1",
            publications: new[] { "com.example.changed.v1" });
        var authorized = Register(
            registry,
            "authorized",
            "business-1",
            subscriptions: new[] { "com.example.changed.v1" });
        var otherBusiness = Register(
            registry,
            "other-business",
            "business-2",
            subscriptions: new[] { "com.example.changed.v1" });

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
    public async Task PublishEvent_WithInstallationSubject_DeliversOnlyToTargetInstance()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var source = Register(registry, "source", "business-1", publications: new[] { "chat.v1" });
        var target = Register(registry, "same-agent", "business-1", subscriptions: new[] { "chat.v1" }, installationId: "target-instance");
        var sibling = Register(registry, "same-agent", "business-1", subscriptions: new[] { "chat.v1" }, installationId: "sibling-instance");

        registry.PublishEvent(source, new PublishEvent
        {
            EventType = "chat.v1",
            Subject = "agent-installation/target-instance/conversation/1"
        }, "correlation-targeted");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.Event, (await target.Outbound.ReadAsync(timeout.Token)).PayloadCase);
        Assert.False(sibling.Outbound.TryRead(out _));
    }

    [Fact]
    public async Task PublishEvent_WithInstallationRoutePermission_CanTargetAnotherBusiness()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var source = Register(registry, "platform", "default", publications: new[] { "chat.v1" }, permissions: new[] { "installation.route" });
        var target = Register(registry, "agent", "business-2", subscriptions: new[] { "chat.v1" }, installationId: "target-instance");

        registry.PublishEvent(source, new PublishEvent
        {
            EventType = "chat.v1",
            Subject = "agent-installation/target-instance/conversation/1"
        }, "cross-business-turn");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var delivered = await target.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.Event, delivered.PayloadCase);
        Assert.False(source.Outbound.TryRead(out _));
    }

    [Fact]
    public async Task PublishEvent_WithNoSubscriber_ReturnsCorrelatedErrorToPublisher()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var source = Register(registry, "source", "business-1", publications: new[] { "chat.v1" });

        registry.PublishEvent(source, new PublishEvent
        {
            EventType = "chat.v1",
            Subject = "conversation/1"
        }, "turn-correlation");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var error = await source.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.Error, error.PayloadCase);
        Assert.Equal("turn-correlation", error.CorrelationId);
        Assert.Equal("event_undelivered", error.Error.Code);
    }

    [Fact]
    public async Task RequestCapability_WithInstallationSelector_UsesTargetInstance()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "requester", "business-1", requestedCapabilities: new[] { "configure.v1" });
        var target = Register(registry, "same-agent", "business-1", capabilities: new[] { "configure.v1" }, installationId: "target-instance");
        var sibling = Register(registry, "same-agent", "business-1", capabilities: new[] { "configure.v1" }, installationId: "sibling-instance");

        registry.RequestCapability(requester, new RequestCapability
        {
            RequestId = "targeted-request",
            Capability = "configure.v1",
            TargetAgentId = "installation:target-instance"
        }, "correlation-targeted");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.CapabilityRequest, (await target.Outbound.ReadAsync(timeout.Token)).PayloadCase);
        Assert.False(sibling.Outbound.TryRead(out _));
    }

    [Fact]
    public async Task RequestCapability_WithInstallationRoutePermission_CanTargetAnotherBusiness()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "platform", "default", permissions: new[] { "installation.route" }, requestedCapabilities: new[] { "configure.v1" });
        var target = Register(registry, "agent", "business-2", capabilities: new[] { "configure.v1" }, installationId: "target-instance");

        registry.RequestCapability(requester, new RequestCapability
        {
            RequestId = "cross-business-request",
            Capability = "configure.v1",
            TargetAgentId = "installation:target-instance"
        }, "cross-business-correlation");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.CapabilityRequest, (await target.Outbound.ReadAsync(timeout.Token)).PayloadCase);
    }

    [Fact]
    public async Task RequestCapability_DeniesCapabilityNotDeclaredInRequestedGrant()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "requester", "business-1", requestedCapabilities: new[] { "allowed.v1" });
        var provider = Register(registry, "provider", "business-1", capabilities: new[] { "denied.v1" });

        registry.RequestCapability(requester, new RequestCapability
        {
            RequestId = "undeclared-request",
            Capability = "denied.v1"
        }, "undeclared-correlation");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var response = await requester.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(BrokerToAgentMessage.PayloadOneofCase.CapabilityResult, response.PayloadCase);
        Assert.False(response.CapabilityResult.Succeeded);
        Assert.Contains("may not request", response.CapabilityResult.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(provider.Outbound.TryRead(out _));
    }

    [Fact]
    public async Task CapabilityResult_ReturnsOnlyFromBrokerSelectedProvider()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "requester", "business-1", requestedCapabilities: new[] { "example.lookup.v1" });
        var provider = Register(
            registry,
            "provider",
            "business-1",
            capabilities: new[] { "example.lookup.v1" });

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

    [Fact]
    public async Task PlatformCapabilityInvocation_TargetsExactInstallationAndReturnsResult()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var provider = Register(
            registry,
            "provider",
            "business-1",
            capabilities: new[] { "configure.v1" },
            installationId: "target-installation");
        _ = Register(
            registry,
            "provider",
            "business-1",
            capabilities: new[] { "configure.v1" },
            installationId: "sibling-installation");

        var invocation = registry.InvokeInstallationCapabilityAsync(
            "business-1",
            "target-installation",
            new RequestCapability
            {
                RequestId = "platform-request",
                Capability = "configure.v1",
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{}")
            },
            CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var request = await provider.Outbound.ReadAsync(timeout.Token);
        Assert.Equal("platform.csweet", request.CapabilityRequest.RequestingAgentId);
        registry.CompleteCapability(provider, new CapabilityResult
        {
            RequestId = "platform-request",
            Succeeded = true,
            ContentType = "application/json",
            Payload = ByteString.CopyFromUtf8("{\"configured\":true}")
        }, request.CorrelationId);

        var result = await invocation.WaitAsync(timeout.Token);
        Assert.True(result.Succeeded);
        Assert.Equal("{\"configured\":true}", result.Payload.ToStringUtf8());
    }

    [Fact]
    public async Task CapabilityResult_FromUnselectedAgent_IsRejectedWithoutConsumingRequest()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var requester = Register(registry, "requester", "business-1", requestedCapabilities: new[] { "example.lookup.v1" });
        var selectedProvider = Register(
            registry,
            "selected-provider",
            "business-1",
            capabilities: new[] { "example.lookup.v1" });
        var attacker = Register(registry, "attacker", "business-1");

        registry.RequestCapability(
            requester,
            new RequestCapability
            {
                RequestId = "request-2",
                Capability = "example.lookup.v1",
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{}")
            },
            "correlation-2");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        _ = await selectedProvider.Outbound.ReadAsync(timeout.Token);

        registry.CompleteCapability(
            attacker,
            new CapabilityResult
            {
                RequestId = "request-2",
                Succeeded = true,
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{\"spoofed\":true}")
            },
            "correlation-2");

        var rejection = await attacker.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(
            BrokerToAgentMessage.PayloadOneofCase.Error,
            rejection.PayloadCase);
        Assert.Equal("capability_result_denied", rejection.Error.Code);
        Assert.False(requester.Outbound.TryRead(out _));

        registry.CompleteCapability(
            selectedProvider,
            new CapabilityResult
            {
                RequestId = "request-2",
                Succeeded = true,
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{\"value\":42}")
            },
            "correlation-2");

        var response = await requester.Outbound.ReadAsync(timeout.Token);
        Assert.True(response.CapabilityResult.Succeeded);
        Assert.Equal("{\"value\":42}", response.CapabilityResult.Payload.ToStringUtf8());
    }

    [Fact]
    public async Task PublishPlatformEvent_TargetsOnlyConfiguredInstallation()
    {
        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var target = Register(registry, "manager", "business-1", subscriptions: new[] { "status.v1" }, installationId: "target");
        var sibling = Register(registry, "manager", "business-1", subscriptions: new[] { "status.v1" }, installationId: "sibling");

        var count = registry.PublishPlatformEvent("business-1", "status.v1", "executive-briefing/1",
            ByteString.CopyFromUtf8("{}"), "correlation", "target");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.Equal(1, count);
        Assert.Equal("platform.csweet", (await target.Outbound.ReadAsync(timeout.Token)).Event.SourceAgentId);
        Assert.False(sibling.Outbound.TryRead(out _));
    }

    private static AgentSession Register(
        AgentSessionRegistry registry,
        string agentId,
        string businessId,
        IEnumerable<string>? capabilities = null,
        IEnumerable<string>? subscriptions = null,
        IEnumerable<string>? publications = null,
        IEnumerable<string>? permissions = null,
        IEnumerable<string>? requestedCapabilities = null,
        string? installationId = null) =>
        registry.Register(
            new RegisterAgent
            {
                AgentId = agentId,
                AgentVersion = "1.0.0",
                InstallationId = installationId ?? $"{agentId}-installation",
                BusinessId = businessId
            },
            new AuthorizedAgentGrant(
                (capabilities ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
                (subscriptions ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
                (publications ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
                (permissions ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal),
                (requestedCapabilities ?? Enumerable.Empty<string>()).ToHashSet(StringComparer.Ordinal)));
}
