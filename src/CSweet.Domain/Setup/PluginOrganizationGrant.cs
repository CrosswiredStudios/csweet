namespace CSweet.Domain.Setup;

/// <summary>
/// Server-owned organization scope for a system plugin. The workload cannot expand this list
/// through its manifest or registration payload.
/// </summary>
public sealed class PluginOrganizationGrant
{
    public Guid Id { get; set; }
    public Guid PluginInstallationId { get; set; }
    public Guid OrganizationId { get; set; }
    public DateTimeOffset GrantedAt { get; set; }

    public AgentInstallation? PluginInstallation { get; set; }
}
