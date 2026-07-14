namespace CSweet.Application.Setup;

public interface IGitHubAgentRepositoryClient
{
    Task<string> GetDefaultBranchAsync(
        string repositoryOwner,
        string repositoryName,
        CancellationToken cancellationToken);

    Task<string> ResolveCommitShaAsync(
        string repositoryOwner,
        string repositoryName,
        string reference,
        CancellationToken cancellationToken);

    Task<byte[]> GetRootManifestAsync(
        string repositoryOwner,
        string repositoryName,
        string commitSha,
        CancellationToken cancellationToken);
}