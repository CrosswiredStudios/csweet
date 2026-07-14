using System.Text;
using System.Text.Json;
using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentInstallationConfigurationService(
    CSweetDbContext dbContext,
    IAuditEventWriter auditWriter) : IAgentInstallationConfigurationService
{
    private const int MaximumSettingsBytes = 256 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentInstallationConfigurationSnapshot?> GetAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await dbContext.AgentInstallationConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.AgentInstallationId == installationId, cancellationToken);

        return configuration is null ? null : ToSnapshot(configuration);
    }

    public async Task<AgentInstallationConfigurationSnapshot> SaveAsync(
        Guid installationId,
        string schemaVersion,
        IReadOnlyDictionary<string, JsonElement> settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schemaVersion) || schemaVersion.Length > 64)
        {
            throw new AgentInstallationException("Agent configuration schema version is required and cannot exceed 64 characters.");
        }

        if (settings.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new AgentInstallationException("Agent configuration keys cannot be empty.");
        }

        if (!await dbContext.AgentInstallations.AnyAsync(x => x.Id == installationId, cancellationToken))
        {
            throw new AgentInstallationException("The agent installation was not found.");
        }

        var settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);
        if (Encoding.UTF8.GetByteCount(settingsJson) > MaximumSettingsBytes)
        {
            throw new AgentInstallationException($"Agent configuration cannot exceed {MaximumSettingsBytes / 1024} KB.");
        }

        var now = DateTimeOffset.UtcNow;
        var configuration = await dbContext.AgentInstallationConfigurations
            .SingleOrDefaultAsync(x => x.AgentInstallationId == installationId, cancellationToken);
        if (configuration is null)
        {
            configuration = new AgentInstallationConfiguration
            {
                Id = Guid.NewGuid(),
                AgentInstallationId = installationId,
                CreatedAt = now
            };
            dbContext.AgentInstallationConfigurations.Add(configuration);
        }

        configuration.SchemaVersion = schemaVersion.Trim();
        configuration.SettingsJson = settingsJson;
        configuration.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditWriter.WriteAsync(
            "agent-installation.configuration.updated",
            nameof(AgentInstallation),
            installationId,
            "Updated persisted agent installation configuration.",
            cancellationToken: cancellationToken);

        return ToSnapshot(configuration);
    }

    private static AgentInstallationConfigurationSnapshot ToSnapshot(
        AgentInstallationConfiguration configuration) =>
        new(
            configuration.AgentInstallationId,
            configuration.SchemaVersion,
            JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(
                configuration.SettingsJson,
                SerializerOptions) ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            configuration.CreatedAt,
            configuration.UpdatedAt);
}