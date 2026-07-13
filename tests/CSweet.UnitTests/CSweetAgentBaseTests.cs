using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Contracts.Agents;
using Google.Protobuf;

namespace CSweet.UnitTests;

public sealed class CSweetAgentBaseTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DescribeConfiguration_ReturnsSdkDefinedSchema()
    {
        var agent = new TestAgent();

        var result = await agent.ExecuteCapabilityAsync(
            new CapabilityRequest
            {
                Capability = AgentConfigurationCapabilities.Describe,
                ContentType = "application/json"
            },
            new AgentRuntimeContext("business-1", "agent-1", new NoopBrokerClient()),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var schema = JsonSerializer.Deserialize<AgentConfigurationSchemaResponse>(
            result.Payload,
            SerializerOptions);

        Assert.NotNull(schema);
        Assert.Equal("com.csweet.test-agent", schema.AgentId);
        Assert.Contains(schema.Fields, field => field.Key == "llmProviderId" && field.Type == AgentConfigurationFieldTypes.LlmProvider);
        Assert.Contains(schema.Fields, field => field.Key == "mode" && field.Type == AgentConfigurationFieldTypes.Select);
        Assert.Equal("balanced", schema.Settings["mode"].GetString());
    }

    [Fact]
    public async Task UpdateConfiguration_RejectsUnknownSetting()
    {
        var agent = new TestAgent();
        var request = new UpdateAgentConfigurationRequest(new Dictionary<string, JsonElement>
        {
            ["unsupported"] = JsonSerializer.SerializeToElement("value")
        });

        var result = await agent.ExecuteCapabilityAsync(
            new CapabilityRequest
            {
                Capability = AgentConfigurationCapabilities.Update,
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(request, SerializerOptions))
            },
            new AgentRuntimeContext("business-1", "agent-1", new NoopBrokerClient()),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("not supported", result.Error);
    }

    [Fact]
    public async Task UpdateConfiguration_StoresValuesForCustomCapabilities()
    {
        var agent = new TestAgent();
        var context = new AgentRuntimeContext("business-1", "agent-1", new NoopBrokerClient());
        var update = new UpdateAgentConfigurationRequest(new Dictionary<string, JsonElement>
        {
            ["llmProviderId"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString()),
            ["llmModel"] = JsonSerializer.SerializeToElement("model-a"),
            ["mode"] = JsonSerializer.SerializeToElement("detailed"),
            ["maxItems"] = JsonSerializer.SerializeToElement(6)
        });

        var updateResult = await agent.ExecuteCapabilityAsync(
            new CapabilityRequest
            {
                Capability = AgentConfigurationCapabilities.Update,
                ContentType = "application/json",
                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(update, SerializerOptions))
            },
            context,
            CancellationToken.None);

        Assert.True(updateResult.Succeeded);

        var readResult = await agent.ExecuteCapabilityAsync(
            new CapabilityRequest
            {
                Capability = TestAgent.ReadSettingsCapability,
                ContentType = "application/json"
            },
            context,
            CancellationToken.None);

        Assert.True(readResult.Succeeded);
        var snapshot = JsonSerializer.Deserialize<TestSettingsSnapshot>(
            readResult.Payload,
            SerializerOptions);

        Assert.NotNull(snapshot);
        Assert.Equal("detailed", snapshot.Mode);
        Assert.Equal(6, snapshot.MaxItems);
    }

    private sealed class TestAgent : CSweetAgentBase
    {
        public const string ReadSettingsCapability = "test.settings.read.v1";

        public override string AgentId => "com.csweet.test-agent";

        public override string Version => "1.0.0";

        protected override AgentConfigurationBuilder Configure(AgentConfigurationBuilder builder)
        {
            return builder
                .LlmProvider("llmProviderId", "LLM Provider", required: true)
                .LlmModel("llmModel", "Model", dependsOnFieldKey: "llmProviderId", required: true)
                .Select(
                    "mode",
                    "Mode",
                    [
                        new AgentConfigurationOption("balanced", "Balanced"),
                        new AgentConfigurationOption("detailed", "Detailed")
                    ],
                    required: true,
                    defaultValue: "balanced")
                .Number("maxItems", "Max Items", required: true, minimum: 1, maximum: 10, defaultValue: 4);
        }

        protected override Task<AgentCapabilityExecutionResult> ExecuteCapabilityCoreAsync(
            CapabilityRequest request,
            AgentRuntimeContext context,
            CancellationToken cancellationToken)
        {
            if (request.Capability != ReadSettingsCapability)
            {
                return base.ExecuteCapabilityCoreAsync(request, context, cancellationToken);
            }

            var snapshot = new TestSettingsSnapshot(
                Settings.GetString("mode"),
                Settings.GetInt32("maxItems"));

            return Task.FromResult(AgentCapabilityExecutionResult.Success(SerializePayload(snapshot)));
        }
    }

    private sealed record TestSettingsSnapshot(string Mode, int MaxItems);

    private sealed class NoopBrokerClient : IAgentBrokerClient
    {
        public Task StartAsync(RegisterAgent registration, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task PublishEventAsync(
            PublishEvent message,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<CapabilityResult> InvokeCapabilityAsync(
            RequestCapability request,
            string? correlationId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CapabilityResult
            {
                RequestId = request.RequestId,
                Succeeded = false,
                Error = "No broker available."
            });

        public Task SendCapabilityResultAsync(
            CapabilityResult result,
            string? correlationId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
