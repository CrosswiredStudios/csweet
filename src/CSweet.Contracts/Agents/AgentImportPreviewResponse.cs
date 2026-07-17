namespace CSweet.Contracts.Agents;

public sealed record AgentImportPreviewResponse(
    Guid ImportId,
    string RepositoryUrl,
    string CommitSha,
    string ManifestDigest,
    string AgentId,
    string AgentName,
    string AgentVersion,
    string PublisherId,
    string PublisherName,
    string RuntimeType,
    string? ProjectPath,
    string? TargetFramework,
    string? DefaultActivationMode,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> RequestedSubscriptions,
    IReadOnlyList<string> RequestedPublications,
    IReadOnlyList<string> RequestedPermissions,
    IReadOnlyList<string> RequestedNetworkAccess,
    IReadOnlyList<AgentManifestWarningResponse> Warnings,
    string Status)
{
    public string PluginKind { get; init; } = "Agent";
    public string ManifestFileName { get; init; } = "csweet-agent.json";
    public IReadOnlyList<string> RequestedCapabilities { get; init; } = [];
}
