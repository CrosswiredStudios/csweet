namespace CSweet.Domain.Planning;

public sealed class PlanningTask
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public CSweet.Domain.Core.Organization? Organization { get; set; }
    public string TaskKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public PlanningTaskStatus Status { get; set; }
    public Guid? ProviderProfileId { get; set; }
    public string? AgentKey { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public string? OutputContent { get; set; }
    public string? OutputStructuredJson { get; set; }
    public string? FailureMessage { get; set; }
    public int? InputTokenCount { get; set; }
    public int? OutputTokenCount { get; set; }
    public long? DurationMs { get; set; }
    public int SortOrder { get; set; }
    public bool IsRequired { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
