using System.Text.Json;

namespace CSweet.Contracts.Core;

public sealed record StartChatTurnRequest(string Message);

public sealed record ChatTurnResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ConversationId,
    Guid UserMessageId,
    Guid? AssistantMessageId,
    string Status,
    int Attempt,
    string PartialResponse,
    string? ErrorCode,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FirstOutputAt,
    DateTimeOffset? ResponseReadyAt,
    DateTimeOffset? CompletedAt,
    long LastSequence);

public sealed record ChatTurnTraceEventResponse(
    Guid Id,
    Guid ChatTurnId,
    long Sequence,
    string Category,
    string EventType,
    string Status,
    string Title,
    string? Summary,
    JsonElement? Details,
    string Sensitivity,
    long? DurationMs,
    DateTimeOffset OccurredAt);

public sealed record ChatTurnStartResponse(ChatTurnResponse Turn, ConversationMessageResponse UserMessage);
