namespace CSweet.Domain.Setup;

public sealed class AgentInstallationGrant
{
    public Guid Id { get; set; }
    public Guid AgentInstallationId { get; set; }
    public string CapabilitiesJson { get; set; } = "[]";
    public string RequestedCapabilitiesJson { get; set; } = "[]";
    public string SubscriptionsJson { get; set; } = "[]";
    public string PublicationsJson { get; set; } = "[]";
    public string PermissionsJson { get; set; } = "[]";
    public string NetworkAccessJson { get; set; } = "[]";
    public int MaxRuntimeSeconds { get; set; }
    public int MemoryMb { get; set; }
    public int CpuPercent { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }

    public AgentInstallation? AgentInstallation { get; set; }
}
