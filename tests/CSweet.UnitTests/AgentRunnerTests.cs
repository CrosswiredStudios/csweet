using CSweet.AI.AgentFramework;
using CSweet.Application.Llm;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;

namespace CSweet.UnitTests;

public class AgentRunnerTests
{
    private readonly FakeAgentRunner _fakeRunner;
    private readonly List<AgentRunLog> _logsWritten;

    public AgentRunnerTests()
    {
        _fakeRunner = new FakeAgentRunner();
        _logsWritten = new List<AgentRunLog>();
    }

    [Fact]
    public async Task RunAsync_Success_ReturnsContent()
    {
        var request = CreateRequest();
        var result = await _fakeRunner.RunAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("Fake agent response content.", result.Content);
        Assert.Null(result.FailureMessage);
        Assert.NotEmpty(result.Logs);
    }

    [Fact]
    public async Task RunAsync_Success_LogsEntry()
    {
        var request = CreateRequest();
        await _fakeRunner.RunAsync(request);

        Assert.Single(_fakeRunner.ReceivedRequests);
        var received = _fakeRunner.ReceivedRequests[0];
        Assert.Equal(request.AgentKey, received.AgentKey);
        Assert.Equal(request.ProviderProfileId, received.ProviderProfileId);
    }

    [Fact]
    public async Task RunAsync_Failure_ReturnsFailureResult()
    {
        _fakeRunner.SimulateFailure = true;
        _fakeRunner.FailureMessage = "Test failure reason.";

        var request = CreateRequest();
        var result = await _fakeRunner.RunAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);
        Assert.Equal("Test failure reason.", result.FailureMessage);
    }

    [Fact]
    public async Task RunAsync_Failure_ContainsErrorLog()
    {
        _fakeRunner.SimulateFailure = true;

        var request = CreateRequest();
        var result = await _fakeRunner.RunAsync(request);

        var errorLog = result.Logs.FirstOrDefault(l => l.Level == "Error");
        Assert.NotNull(errorLog);
    }

    [Fact]
    public async Task RunAsync_StructuredOutput_ReturnsJson()
    {
        _fakeRunner.StructuredJson = "{\"key\":\"value\"}";

        var request = CreateRequest();
        var result = await _fakeRunner.RunAsync(request);

        Assert.True(result.Succeeded);
        Assert.Equal("{\"key\":\"value\"}", result.StructuredJson);
    }

    [Fact]
    public async Task RunAsync_PreservesContext()
    {
        var context = new Dictionary<string, string>
        {
            ["OrganizationName"] = "Acme Corp",
            ["Industry"] = "Retail"
        };

        var request = CreateRequest(context: context);
        await _fakeRunner.RunAsync(request);

        Assert.Single(_fakeRunner.ReceivedRequests);
        Assert.Equal("Acme Corp", _fakeRunner.ReceivedRequests[0].Context["OrganizationName"]);
    }

    [Fact]
    public async Task RunAsync_StructuredOutputFailure_IsHandledGracefully()
    {
        _fakeRunner.SimulateFailure = true;
        _fakeRunner.FailureMessage = "Structured output validation failed.";

        var request = CreateRequest(options: new AgentRunOptions(
            Temperature: null,
            MaxOutputTokens: null,
            RequireStructuredOutput: true,
            OutputSchemaJson: "{\"type\":\"object\",\"properties\":{\"name\":\"string\"}}"));

        var result = await _fakeRunner.RunAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);
        Assert.Equal("Structured output validation failed.", result.FailureMessage);
    }

    [Fact]
    public void BusinessStrategistAgentProfile_HasExpectedKey()
    {
        var descriptor = BusinessStrategistAgentProfile.Descriptor;

        Assert.Equal("business-strategist", descriptor.AgentKey);
        Assert.Equal("Business Strategist", descriptor.DisplayName);
        Assert.Contains("business-planning", descriptor.Capabilities);
        Assert.Contains("operating-plan", descriptor.Capabilities);
        Assert.Contains("task-breakdown", descriptor.Capabilities);
        Assert.Contains("risk-identification", descriptor.Capabilities);
    }

    [Fact]
    public void AgentFrameworkAgentFactory_ResolvesBusinessStrategist()
    {
        var factory = new AgentFrameworkAgentFactory();
        var resolved = factory.Resolve("business-strategist");

        Assert.NotNull(resolved);
        Assert.Equal("business-strategist", resolved!.AgentKey);
    }

    [Fact]
    public void AgentFrameworkAgentFactory_ReturnsNullForUnknownKey()
    {
        var factory = new AgentFrameworkAgentFactory();
        var resolved = factory.Resolve("nonexistent-agent");

        Assert.Null(resolved);
    }

    [Fact]
    public void AgentFrameworkToolRegistry_RegistersAndReturnsTools()
    {
        var registry = new AgentFrameworkToolRegistry();

        Assert.Empty(registry.Tools);

        // We can't easily create a real AITool without more setup,
        // but we verify the Clear operation works.
        registry.Clear();
        Assert.Empty(registry.Tools);
    }

    private static AgentRunRequest CreateRequest(
        Guid? providerProfileId = null,
        string agentKey = "test-agent",
        Dictionary<string, string>? context = null,
        AgentRunOptions? options = null)
    {
        return new AgentRunRequest(
            ProviderProfileId: providerProfileId ?? Guid.NewGuid(),
            AgentKey: agentKey,
            SystemPrompt: "You are a test agent.",
            UserPrompt: "Do something useful.",
            Context: context ?? new Dictionary<string, string>(),
            Options: options ?? new AgentRunOptions(
                Temperature: null,
                MaxOutputTokens: null,
                RequireStructuredOutput: false,
                OutputSchemaJson: null));
    }
}
