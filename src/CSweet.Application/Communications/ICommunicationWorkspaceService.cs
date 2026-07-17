using CSweet.Communications.Abstractions;
using CSweet.Contracts.Communications;

namespace CSweet.Application.Communications;

public interface ICommunicationWorkspaceService
{
    Task<CommunicationConnectionResponse?> GetAsync(Guid organizationId, string providerKey, CancellationToken cancellationToken = default);
    Task<CommunicationConnectionResponse> ConnectAsync(Guid organizationId, string providerKey, ConnectCommunicationWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceProvisioningPlan?> PreviewAsync(Guid organizationId, string providerKey, CancellationToken cancellationToken = default);
    Task<CommunicationActionResponse> QueueReconciliationAsync(Guid organizationId, string providerKey, CancellationToken cancellationToken = default);
    Task<CommunicationActionResponse> DisconnectAsync(Guid organizationId, string providerKey, CancellationToken cancellationToken = default);
    Task<CommunicationConnectionResponse?> GetDiscordAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<CommunicationConnectionResponse> ConnectDiscordAsync(Guid organizationId, ConnectDiscordWorkspaceRequest request, CancellationToken cancellationToken = default);
    Task<WorkspaceProvisioningPlan?> PreviewAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<CommunicationActionResponse> QueueReconciliationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<CommunicationActionResponse> DisconnectDiscordAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<LinkCodeResponse?> CreateLinkCodeAsync(Guid organizationId, Guid applicationUserId, CancellationToken cancellationToken = default);
    Task<ExternalIdentityLinkResponse?> RedeemLinkCodeAsync(RedeemExternalIdentityRequest request, CancellationToken cancellationToken = default);
    Task<CommunicationActionResponse> SelectDirectAgentAsync(Guid organizationId, Guid applicationUserId, Guid? agentOrganizationUserId, CancellationToken cancellationToken = default);
}

public interface ICommunicationRouter
{
    Task<CommunicationActionResponse> RouteInboundAsync(NormalizedCommunicationEnvelope envelope, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> ListAsync(Guid organizationId, Guid applicationUserId, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(Guid organizationId, Guid applicationUserId, Guid notificationId, CancellationToken cancellationToken = default);
}
