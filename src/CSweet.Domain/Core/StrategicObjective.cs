namespace CSweet.Domain.Core;

public sealed class StrategicObjective
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ObjectiveStatus Status { get; set; }
    public DateTimeOffset? TargetDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
}
