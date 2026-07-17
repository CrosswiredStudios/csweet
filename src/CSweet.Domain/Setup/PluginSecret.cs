namespace CSweet.Domain.Setup;

/// <summary>Encrypted, installation-scoped secret metadata. Plaintext is never persisted.</summary>
public sealed class PluginSecret
{
    public Guid Id { get; set; }
    public Guid PluginInstallationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ProtectedValue { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AgentInstallation? PluginInstallation { get; set; }
}
