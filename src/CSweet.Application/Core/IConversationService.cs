using CSweet.Contracts.Core;
using CSweet.Domain.Core;

namespace CSweet.Application.Core;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationResponse>> ListAsync(
        Guid organizationId,
        Guid agentOrganizationUserId,
        CancellationToken cancellationToken = default);

    Task<ConversationResponse?> GetAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessageResponse>> ListMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetDefaultProviderProfileIdAsync(CancellationToken cancellationToken = default);

    Task<bool> IsProviderProfileEnabledAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetAgentInstallationIdAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<Guid?> GetAgentInstallationIdForEmployeeAsync(Guid organizationUserId, CancellationToken cancellationToken = default);

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
