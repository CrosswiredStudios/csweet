namespace CSweet.Contracts.Core;

public sealed record ConversationMessageResponse(
    Guid Id,
    Guid ConversationId,
    int Role,          // 0 = User, 1 = Assistant (matches ConversationRole)
    string Content,
    DateTimeOffset CreatedAt,
    Guid? ChatTurnId = null);
