namespace CSweet.Domain.Core;

/// <summary>
/// Represents a business work task. Named WorkTask to avoid conflict with System.Threading.Tasks.Task.
/// </summary>
public sealed class WorkTask
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? StrategicObjectiveId { get; set; }
    public Guid? AssignedRoleId { get; set; }
    public Guid? AssignedWorkerId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkTaskStatus Status { get; set; }
    public WorkTaskPriority Priority { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public bool RequiresApproval { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public StrategicObjective? StrategicObjective { get; set; }
    public Role? AssignedRole { get; set; }
    public Worker? AssignedWorker { get; set; }
}
