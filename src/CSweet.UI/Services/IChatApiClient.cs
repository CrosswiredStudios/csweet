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
}
