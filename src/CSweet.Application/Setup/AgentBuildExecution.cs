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
    int MaximumBuildLogMb);

public sealed record AgentBuildWorkspace(
    string SourcePath,
    string StagingPackagePath,
    string LogPath);

public sealed record AgentBuildExecutionResult(
    string PackagePath,
    string PackageDigest,
    string LogPath);

public interface IAgentBuildExecutor
{
    Task<AgentBuildWorkspace> CloneAsync(
        AgentBuildExecutionRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentBuildExecutionResult> BuildAsync(
        AgentBuildExecutionRequest request,
        AgentBuildWorkspace workspace,
        CancellationToken cancellationToken = default);

    Task CleanupWorkspaceAsync(
        AgentBuildWorkspace workspace,
        CancellationToken cancellationToken = default);
}
