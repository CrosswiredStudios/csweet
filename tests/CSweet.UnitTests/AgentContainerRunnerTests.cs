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
            new DockerCommandResult(0, "container-id\n", string.Empty),
            new DockerCommandResult(0, InspectJson, string.Empty));
        var runner = new DockerAgentContainerRunner(docker, NullLogger<DockerAgentContainerRunner>.Instance);

        var status = await runner.StartAsync(CreateRequest());

        Assert.Equal(AgentContainerState.Running, status.State);
        var args = docker.Commands[0];
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
        Assert.Equal(10, args.Count(value => value == "--env"));
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
        "/app/csweet-agent.json", "csweet-broker", 512, 50, 100, 600);

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
        public Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }
}
