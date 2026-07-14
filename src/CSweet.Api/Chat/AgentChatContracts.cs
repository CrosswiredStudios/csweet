namespace CSweet.Api.Chat;

internal static class AgentChatEvents
{
    public const string UserMessageReceivedEvent = "com.csweet.user.message.received.v1";

    public const string AssistantResponseCreatedEvent = "com.csweet.assistant.response.created.v1";

    public const string AssistantResponseChunkEvent = "com.csweet.assistant.response.chunk.v1";
}

internal sealed record UserMessageReceived(
    Guid ProviderProfileId,
    string ConversationId,
    string UserId,
    string Message,
    IReadOnlyDictionary<string, string>? Context);

internal sealed record AssistantResponseChunk(
    string ConversationId,
    int Sequence,
    string Delta,
    bool IsFinal,
    string? Error = null);
