namespace CSweet.Contracts.Core;

public sealed record ConversationActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    ConversationResponse? Conversation = null);
