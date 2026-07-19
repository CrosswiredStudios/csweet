using CSweet.Application.Setup;
using CSweet.Infrastructure.Setup;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class AgentContainerRunnerTests
{
    [Fact]
    public async Task StartAsync_AppliesLimitsAndOnlyApprovedEnvironment()
    {
        var docker = new FakeDockerCommandExecutor(
            new DockerCommandResult(0, "[]", string.Empty),
            new DockerCommandResult(0, string.Empty, string.Empty),
            new DockerCommandResult(0, "container-id\n", string.Empty),
            new DockerCommandResult(0, InspectJson, string.Empty));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);

        var status = await runner.StartAsync(CreateRequest());

        Assert.Equal(AgentContainerState.Running, status.State);
        Assert.Equal(["network", "inspect", "csweet-broker"], docker.Commands[0]);
        Assert.Equal(["network", "connect", "--alias", "agenthost", "csweet-broker", "agenthost"], docker.Commands[1]);
        var args = docker.Commands[2];
        Assert.Contains("--read-only", args);
        Assert.Contains("ALL", args);
        Assert.Contains("no-new-privileges=true", args);
        Assert.Contains("512m", args);
        Assert.Contains("0.5", args);
        Assert.Contains("100", args);
        Assert.Contains("type=bind,source=C:\\packages\\agent,target=/app,readonly", args);
        Assert.DoesNotContain("--privileged", args);
        Assert.DoesNotContain(args, value => value.Contains("docker.sock", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(args, value => value.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(11, args.Count(value => value == "--env"));
        Assert.Contains("CSweet__Plugin__InstallationId=22222222-2222-2222-2222-222222222222", args);
        Assert.Contains(args, value => value.StartsWith("CSweet__Plugin__BrokerEndpoint=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartAsync_CreatesMissingRuntimeNetworkBeforeContainer()
    {
        var docker = new FakeDockerCommandExecutor(
            new DockerCommandResult(1, string.Empty, "network csweet-broker not found"),
            new DockerCommandResult(0, "network-id", string.Empty),
            new DockerCommandResult(0, string.Empty, string.Empty),
            new DockerCommandResult(0, "container-id\n", string.Empty),
            new DockerCommandResult(0, InspectJson, string.Empty));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);

        await runner.StartAsync(CreateRequest());

        Assert.Equal(["network", "create", "--driver", "bridge", "--internal", "csweet-broker"], docker.Commands[1]);
        Assert.Equal(["network", "connect", "--alias", "agenthost", "csweet-broker", "agenthost"], docker.Commands[2]);
        Assert.Equal("run", docker.Commands[3][0]);
    }

    [Fact]
    public async Task RemoveNetworkAsync_DetachesBrokerAndRemovesRuntimeNetwork()
    {
        var docker = new FakeDockerCommandExecutor(
            new DockerCommandResult(0, "[]", string.Empty),
            new DockerCommandResult(0, string.Empty, string.Empty),
            new DockerCommandResult(0, "csweet-broker", string.Empty));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);

        await runner.RemoveNetworkAsync("csweet-broker", "agenthost");

        Assert.Equal(["network", "inspect", "csweet-broker"], docker.Commands[0]);
        Assert.Equal(["network", "disconnect", "--force", "csweet-broker", "agenthost"], docker.Commands[1]);
        Assert.Equal(["network", "rm", "csweet-broker"], docker.Commands[2]);
    }

    [Fact]
    public async Task RemoveNetworkAsync_MissingNetworkIsAlreadyClean()
    {
        var docker = new FakeDockerCommandExecutor(
            new DockerCommandResult(1, string.Empty, "Error: No such network: csweet-broker"));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);

        await runner.RemoveNetworkAsync("csweet-broker", "agenthost");

        Assert.Single(docker.Commands);
    }

    [Fact]
    public void RuntimeOptions_DefaultToContainerizedBrokerGateway()
    {
        var options = new AgentRuntimeManagerOptions();

        Assert.Equal("http://agenthost:8080", options.BrokerEndpoint);
        Assert.Equal("agenthost", options.BrokerGatewayContainer);
    }

    [Fact]
    public async Task StartAsync_UsesEndpointHostAsAliasForConfiguredGatewayContainer()
    {
        var docker = new FakeDockerCommandExecutor(
            new DockerCommandResult(0, "[]", string.Empty),
            new DockerCommandResult(0, string.Empty, string.Empty),
            new DockerCommandResult(0, "container-id\n", string.Empty),
            new DockerCommandResult(0, InspectJson, string.Empty));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);
        var request = CreateRequest() with
        {
            BrokerEndpoint = "http://csweet-agenthost:8080",
            BrokerGatewayContainer = "agenthost"
        };

        await runner.StartAsync(request);

        Assert.Equal(
            ["network", "connect", "--alias", "csweet-agenthost", "csweet-broker", "agenthost"],
            docker.Commands[1]);
    }

    [Fact]
    public async Task StartAsync_RejectsUnsafeEntryAssemblyBeforeDockerRuns()
    {
        var docker = new FakeDockerCommandExecutor();
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);
        var request = CreateRequest() with { EntryAssembly = "../escape.dll" };

        await Assert.ThrowsAsync<AgentContainerException>(() => runner.StartAsync(request));

        Assert.Empty(docker.Commands);
    }

    [Fact]
    public async Task FakeRunner_CanDriveRuntimeManagerTestsWithoutDocker()
    {
        IAgentContainerRunner runner = new FakeAgentContainerRunner();

        var status = await runner.StartAsync(CreateRequest());
        await runner.StopAsync(status.ContainerId, TimeSpan.FromSeconds(5));

        Assert.Equal(AgentContainerState.Running, status.State);
    }

    private static AgentContainerStartRequest CreateRequest() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "com.example.agent", "business-1",
        "csweet-agent-test", "mcr.microsoft.com/dotnet/runtime:9.0",
        "C:\\packages\\agent", "Example.Agent.dll", "http://agenthost:8080", "bounded-token",
        "/app/csweet-plugin.json", "csweet-broker", 512, 50, 100, 600);

    private const string InspectJson = """
        {"Id":"container-id","Name":"/csweet-agent-test","State":{"Status":"running","ExitCode":0,"StartedAt":"2026-07-14T01:02:03Z","FinishedAt":"0001-01-01T00:00:00Z","Error":""}}
        """;

    private sealed class FakeDockerCommandExecutor(params DockerCommandResult[] results) : IDockerCommandExecutor
    {
        private readonly Queue<DockerCommandResult> _results = new(results);
        public List<IReadOnlyList<string>> Commands { get; } = [];

        public Task<DockerCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            Commands.Add(arguments.ToArray());
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FakeAgentContainerRunner : IAgentContainerRunner
    {
        public Task<AgentContainerStatus> StartAsync(AgentContainerStartRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new AgentContainerStatus("fake", request.ContainerName, AgentContainerState.Running, null, DateTimeOffset.UtcNow, null, null));
        public Task StopAsync(string containerId, TimeSpan gracePeriod, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AgentContainerStatus?> InspectAsync(string containerId, CancellationToken cancellationToken = default) => Task.FromResult<AgentContainerStatus?>(null);
        public Task RemoveAsync(string containerId, bool force = false, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveNetworkAsync(string networkName, string brokerGatewayContainer, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }
}
