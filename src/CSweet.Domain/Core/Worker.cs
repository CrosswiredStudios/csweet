namespace CSweet.Domain.Core;

public sealed class Worker
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkerType WorkerType { get; set; }
    public WorkerExecutionMode ExecutionMode { get; set; }
    public string CapabilitiesJson { get; set; } = "[]";
    public string? CostModelJson { get; set; }
    public string? EndpointConfigurationJson { get; set; }
    public bool IsEnabled { get; set; }
    public bool RequiresHumanApproval { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
}
