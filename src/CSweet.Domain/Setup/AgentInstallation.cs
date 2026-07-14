namespace CSweet.Domain.Setup;

public sealed class AgentInstallation
{
    public Guid Id { get; set; }
    public Guid PackageVersionId { get; set; }
    public string BusinessId { get; set; } = "default";
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public AgentPackageVersion? PackageVersion { get; set; }
    public AgentInstallationGrant? Grant { get; set; }
    public AgentSchedule? Schedule { get; set; }
    public ICollection<AgentRuntimeInstance> RuntimeInstances { get; set; } = [];
}
