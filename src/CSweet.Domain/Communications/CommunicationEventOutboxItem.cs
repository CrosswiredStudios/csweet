namespace CSweet.Domain.Communications;

public enum CommunicationEventOutboxStatus
{
    Pending,
    Published,
    DeadLettered
}

/// <summary>Transactional event log used to publish conversation mutations to scoped plugins.</summary>
public sealed class CommunicationEventOutboxItem
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ChatId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public CommunicationEventOutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string DeliveredInstallationIdsJson { get; set; } = "[]";
    public string? LastError { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
