using System.Text.Json;

namespace CSweet.Application.Setup;

public interface IAgentInstallationConfigurationService
{
    Task<AgentInstallationConfigurationSnapshot?> GetAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);

    Task<AgentInstallationConfigurationSnapshot> SaveAsync(
        Guid installationId,
        string schemaVersion,
        IReadOnlyDictionary<string, JsonElement> settings,
        CancellationToken cancellationToken = default);
}

public sealed record AgentInstallationConfigurationSnapshot(
    Guid InstallationId,
    string SchemaVersion,
    IReadOnlyDictionary<string, JsonElement> Settings,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);