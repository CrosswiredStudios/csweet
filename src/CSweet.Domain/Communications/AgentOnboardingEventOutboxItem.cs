namespace CSweet.Domain.Communications;

public enum AgentOnboardingEventOutboxStatus
{
    Pending,
    Delivered,
    Failed,
    Cancelled
}

/// <summary>
/// A durable, one-time lifecycle event created with an agent employee and delivered
/// after that specific agent installation connects to the broker for the first time.
/// </summary>
public sealed class AgentOnboardingEventOutboxItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid AgentOrganizationUserId { get; set; }
    public Guid HiringOrganizationUserId { get; set; }
    public Guid ConversationId { get; set; }
    public AgentOnboardingEventOutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
