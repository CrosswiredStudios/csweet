namespace CSweet.Application.Setup;

public sealed record AgentBuildExecutionRequest(
    Guid BuildJobId,
    Guid PackageVersionId,
    string RepositoryUrl,
    string CommitSha,
    string ProjectPath,
    string? TargetFramework,
    string BuilderImage,
    string SourceRootPath,
    string PackageCachePath,
    int TimeoutSeconds,
    int MemoryMb,
    int CpuPercent,
    int PidsLimit,
    int MaximumRepositorySizeMb,
    int MaximumBuildLogMb,
    string? SourceArchivePath = null);

public sealed record AgentBuildWorkspace(
    string SourcePath,
    string StagingPackagePath,
    string LogPath);

public sealed record AgentBuildExecutionResult(
    string PackagePath,
    string PackageDigest,
    string LogPath);

public static class AgentBuildStepKeys
{
    public const string Queued = "queued";
    public const string Source = "source";
    public const string Isolate = "isolate";
    public const string Restore = "restore";
    public const string Publish = "publish";
    public const string Package = "package";
}

public static class AgentBuildStepStatuses
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public sealed record AgentBuildProgressUpdate(
    string StepKey,
    string Status,
    string? Detail = null,
    string? Error = null);

public interface IAgentBuildProgressReporter
{
    Task ReportAsync(
        AgentBuildProgressUpdate update,
        CancellationToken cancellationToken = default);
}

public interface IAgentBuildExecutor
{
    Task<AgentBuildWorkspace> CloneAsync(
        AgentBuildExecutionRequest request,
        IAgentBuildProgressReporter progress,
        CancellationToken cancellationToken = default);

    Task<AgentBuildExecutionResult> BuildAsync(
        AgentBuildExecutionRequest request,
        AgentBuildWorkspace workspace,
        IAgentBuildProgressReporter progress,
        CancellationToken cancellationToken = default);

    Task CleanupWorkspaceAsync(
        AgentBuildWorkspace workspace,
        CancellationToken cancellationToken = default);
}
