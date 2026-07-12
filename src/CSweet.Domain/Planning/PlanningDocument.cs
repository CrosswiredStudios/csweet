namespace CSweet.Domain.Planning;

public sealed class PlanningDocument
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public CSweet.Domain.Core.Organization? Organization { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? StructuredJson { get; set; }
    public string? Summary { get; set; }
    public int Version { get; set; }
    public bool IsLatest { get; set; }
    public Guid? GeneratedByTaskId { get; set; }
    public DateTimeOffset? GeneratedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
