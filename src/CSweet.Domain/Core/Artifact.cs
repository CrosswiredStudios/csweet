namespace CSweet.Domain.Core;

public sealed class Artifact
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? TaskRunId { get; set; }
    public ArtifactType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Version { get; set; }
    public ApprovalStatus ApprovalStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public Organization? Organization { get; set; }
    public WorkTask? Task { get; set; }
    public TaskRun? TaskRun { get; set; }
}
