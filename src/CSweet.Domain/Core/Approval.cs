namespace CSweet.Domain.Core;

public sealed class Approval
{
    public Guid Id { get; set; }
    public Guid ArtifactId { get; set; }
    public ApprovalStatus Status { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Artifact? Artifact { get; set; }
}
