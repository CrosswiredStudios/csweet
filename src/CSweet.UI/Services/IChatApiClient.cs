using CSweet.Contracts.Core;

namespace CSweet.UI.Services;

public interface IChatApiClient
{
    Task<IReadOnlyList<ConversationResponse>> GetConversationsAsync(
        Guid organizationId,
        Guid agentOrganizationUserId,
        CancellationToken cancellationToken = default);

    Task<ConversationResponse> StartConversationAsync(
        Guid organizationId,
        Guid agentOrganizationUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid organizationId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> SendMessageAsync(
        Guid organizationId,
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    Task<ChatTurnStartResponse> StartTurnAsync(Guid organizationId, Guid conversationId, string message, CancellationToken cancellationToken = default);
    Task<ChatTurnResponse?> GetTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatTurnTraceEventResponse>> GetTurnTraceAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatTurnTraceEventResponse> StreamTurnEventsAsync(Guid organizationId, Guid turnId, long afterSequence = -1, CancellationToken cancellationToken = default);
    Task<ChatTurnStartResponse> RetryTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default);
    Task CancelTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default);
}
