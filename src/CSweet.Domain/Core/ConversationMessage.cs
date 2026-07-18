namespace CSweet.Domain.Core;

public sealed class ConversationMessage
{
    public Guid Id { get; set; }
    public long Sequence { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ChatTurnId { get; set; }
    public Guid? SenderOrganizationUserId { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid? CausationId { get; set; }
    public CommunicationDeliveryIntent DeliveryIntent { get; set; } = CommunicationDeliveryIntent.Inform;
    public string SourceProvider { get; set; } = "InApp";
    public string? SourceChannelExternalId { get; set; }
    public string? IdempotencyKey { get; set; }
    public int HopCount { get; set; }

    // Navigation
    public Conversation? Conversation { get; set; }
}

public enum CommunicationDeliveryIntent
{
    Inform,
    RequestResponse,
    Response
}
