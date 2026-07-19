using CSweet.Contracts.Core;
using CSweet.Domain.Core;

namespace CSweet.Application.Core;

public interface IConversationService
{
    Task<Guid?> GetDefaultProviderProfileIdAsync(CancellationToken cancellationToken = default);

    Task<bool> IsProviderProfileEnabledAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetAgentInstallationIdForEmployeeAsync(Guid organizationUserId, CancellationToken cancellationToken = default);

    /// <summary>Appends a message turn and bumps the conversation's UpdatedAt.</summary>
    Task<ConversationMessageResponse> AppendMessageAsync(
        Guid conversationId,
        ConversationRole role,
        string content,
        CancellationToken cancellationToken = default);
}
