using CSweet.Contracts.Agents;

namespace CSweet.Application.Setup;

// These generic ports deliberately extend the legacy agent ports. Existing agents keep their
// API while new plugin kinds depend only on plugin terminology.
public interface IPluginImportService : IAgentImportPreviewService;
public interface IPluginBuildExecutor : IAgentBuildExecutor;
public interface IPluginRuntimeManager : IAgentRuntimeManager;
public interface IPluginContainerRunner : IAgentContainerRunner;

public interface IPluginSourceResolver
{
    Task<string> ResolveCommitShaAsync(string repositoryUrl, string reference, CancellationToken cancellationToken = default);
}

public interface IPluginManifestReader
{
    PluginManifestEnvelope Read(ReadOnlyMemory<byte> manifestBytes, string manifestFileName);
}

public sealed record PluginManifestEnvelope(
    string ManifestFileName,
    string Kind,
    string Id,
    string Name,
    string Version,
    string ManifestJson);

public interface IPluginAuthorizationPolicy
{
    Task<bool> CanAccessOrganizationAsync(
        Guid installationId,
        Guid organizationId,
        CancellationToken cancellationToken = default);
}

public static class PluginPlatformCapabilities
{
    public const string ReadSecret = "plugin.secret.read.v1";
}

public sealed record PluginSecretReadRequest(string Key);
public sealed record PluginSecretReadResponse(string Value);
