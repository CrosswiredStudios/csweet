namespace CSweet.Domain.Core;

public sealed class ConversationMessage
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public ConversationRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? ChatTurnId { get; set; }

    // Navigation
    public Conversation? Conversation { get; set; }
}
