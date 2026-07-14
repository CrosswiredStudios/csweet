namespace CSweet.Agent.Contracts.Packaging;

public sealed class AgentManifest
{
    public string ManifestVersion { get; init; } = "1.0";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required AgentPublisher Publisher { get; init; }

    public required AgentRuntimeManifest Runtime { get; init; }

    public required AgentProtocolManifest Protocol { get; init; }

    public IReadOnlyList<string> Capabilities { get; init; } = [];

    public IReadOnlyList<string> RequestedSubscriptions { get; init; } = [];

    public IReadOnlyList<string> RequestedPublications { get; init; } = [];

    public IReadOnlyList<string> RequestedPermissions { get; init; } = [];

    public IReadOnlyList<string> RequestedNetworkAccess { get; init; } = [];
}

public sealed record AgentPublisher(
    string Id,
    string Name);

public sealed class AgentRuntimeManifest
{
    public string Type { get; init; } = "executable";

    public string? ProjectPath { get; init; }

    public string? TargetFramework { get; init; }

    public string? DefaultActivationMode { get; init; }

    public IReadOnlyDictionary<string, string> Entrypoints { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool SupportsMultipleInstallations { get; init; }

    public int MaximumConcurrentJobs { get; init; } = 1;
}

public sealed record AgentProtocolManifest(
    string MinimumVersion,
    string MaximumVersion);
