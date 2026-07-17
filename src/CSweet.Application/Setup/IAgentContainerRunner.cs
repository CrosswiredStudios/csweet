namespace CSweet.Application.Setup;

public interface IAgentContainerRunner
{
    Task<AgentContainerStatus> StartAsync(AgentContainerStartRequest request, CancellationToken cancellationToken = default);
    Task StopAsync(string containerId, TimeSpan gracePeriod, CancellationToken cancellationToken = default);
    Task<AgentContainerStatus?> InspectAsync(string containerId, CancellationToken cancellationToken = default);
    Task RemoveAsync(string containerId, bool force = false, CancellationToken cancellationToken = default);
    Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default);
}

public sealed record AgentContainerStartRequest(
    Guid RuntimeInstanceId,
    Guid TickId,
    Guid InstallationId,
    string AgentId,
    string BusinessId,
    string ContainerName,
    string RuntimeImage,
    string PackagePath,
    string EntryAssembly,
    string BrokerEndpoint,
    string WorkloadToken,
    string ManifestPath,
    string NetworkName,
    int MemoryMb,
    int CpuPercent,
    int PidsLimit,
    int MaxRuntimeSeconds,
    string? PersistentDataVolumeName = null,
    string BrokerGatewayContainer = "agenthost");

public sealed record AgentContainerStatus(
    string ContainerId,
    string Name,
    AgentContainerState State,
    int? ExitCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Error);

public enum AgentContainerState
{
    Created,
    Running,
    Exited,
    Dead,
    Paused,
    Restarting,
    Unknown
}

public sealed class AgentContainerException : Exception
{
    public AgentContainerException(string message) : base(message) { }
    public AgentContainerException(string message, Exception innerException) : base(message, innerException) { }
}
