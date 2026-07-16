namespace CSweet.Domain.Core;

public enum MemoryCaptureStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public sealed class MemoryCaptureOutboxItem
{
    public Guid Id { get; set; }
    public Guid ConversationMessageId { get; set; }
    public MemoryCaptureStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? EpisodeCapturedAt { get; set; }
    public DateTimeOffset? EnrichedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? LastError { get; set; }

    public ConversationMessage? ConversationMessage { get; set; }
}
