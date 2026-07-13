using CSweet.Contracts.Core;
using CSweet.Domain.Core;

namespace CSweet.Application.Core;

public interface IConversationService
{
    Task<ConversationResponse?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a conversation with the given agent employee, or returns a failure.</summary>
    Task<ConversationActionResponse> StartAsync(
        Guid organizationId,
        StartConversationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Appends a message turn and bumps the conversation's UpdatedAt.</summary>
    Task<ConversationMessageResponse> AppendMessageAsync(
        Guid conversationId,
        ConversationRole role,
        string content,
        CancellationToken cancellationToken = default);
}
