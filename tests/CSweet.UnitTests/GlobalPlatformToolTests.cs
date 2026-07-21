using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using CSweet.Contracts.Agents;
using CSweet.Contracts.Communications;
using CSweet.Contracts.Core;
using Google.Protobuf;

namespace CSweet.UnitTests;

public sealed class GlobalPlatformToolTests
{
    [Fact]
    public void Catalog_ExposesAskUserWithoutRequestedCapabilityGrants()
    {
        var tools = new McpToolCatalog().List(new HashSet<string>(StringComparer.Ordinal));

        var tool = Assert.Single(tools);
        Assert.Equal("ask_user", tool.Name);
        Assert.Equal(CommunicationHubCapabilities.AskUser, tool.Capability);
        Assert.Equal(McpToolAvailability.Global, tool.Availability);
    }

    [Fact]
    public void Catalog_ExposesInstallationScopedHiringBacklogAsReadOnly()
    {
        var tools = new McpToolCatalog().List(new HashSet<string>(StringComparer.Ordinal)
        {
            HiringCapabilities.ListRecommendations
        });

        var tool = Assert.Single(tools, x => x.Name == "list_hiring_recommendations");
        Assert.Equal(HiringCapabilities.ListRecommendations, tool.Capability);
        Assert.Equal(McpToolExecutionPolicy.ReadOnly, tool.ExecutionPolicy);
        Assert.Equal(McpToolAvailability.GrantRequired, tool.Availability);
    }

    [Fact]
    public async Task Dispatcher_AllowsGlobalToolButStillDeniesGrantRequiredTool()
    {
        var handler = new StubHandler();
        var dispatcher = new PlatformCapabilityDispatcher([handler]);
        var session = new AgentSession(
            "session", "agent", Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            "runtime", "tick",
            new AuthorizedAgentGrant(
                new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
                new HashSet<string>(), new HashSet<string>()));

        var global = await InvokeAsync(dispatcher, session, CommunicationHubCapabilities.AskUser);
        var grantRequired = await InvokeAsync(dispatcher, session, CommunicationHubCapabilities.Create);

        Assert.True(global.Succeeded);
        Assert.False(grantRequired.Succeeded);
        Assert.Contains("may not request", grantRequired.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, handler.InvocationCount);
    }

    [Fact]
    public async Task Dispatcher_AllowsOnboardingAcknowledgementWithoutManifestGrant()
    {
        var handler = new StubHandler();
        var dispatcher = new PlatformCapabilityDispatcher([handler]);
        var session = new AgentSession(
            "session", "agent", Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            "runtime", "tick",
            new AuthorizedAgentGrant(
                new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
                new HashSet<string>(), new HashSet<string>()));

        var result = await InvokeAsync(dispatcher, session, AgentLifecycleCapabilities.CompleteOnboarding);

        Assert.True(result.Succeeded);
        Assert.Equal(1, handler.InvocationCount);
    }

    private static async Task<CapabilityResult> InvokeAsync(
        IPlatformCapabilityDispatcher dispatcher,
        AgentSession session,
        string capability)
    {
        await foreach (var result in dispatcher.InvokeAsync(session, new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"),
            Capability = capability,
            ContentType = "application/json",
            Payload = ByteString.CopyFromUtf8("{}")
        }, CancellationToken.None))
            return result;

        throw new InvalidOperationException("The dispatcher returned no result.");
    }

    private sealed class StubHandler : IPlatformCapabilityHandler
    {
        public int InvocationCount { get; private set; }

        public bool CanHandle(string capability) =>
            capability is CommunicationHubCapabilities.AskUser or CommunicationHubCapabilities.Create or
                AgentLifecycleCapabilities.CompleteOnboarding;

        public async IAsyncEnumerable<CapabilityResult> HandleAsync(
            AgentSession session,
            RequestCapability request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            InvocationCount++;
            yield return new CapabilityResult
            {
                RequestId = request.RequestId,
                Succeeded = true,
                ContentType = "application/json",
                Payload = ByteString.CopyFromUtf8("{}")
            };
            await Task.CompletedTask;
        }
    }
}
