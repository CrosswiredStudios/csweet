namespace CSweet.Contracts.Communications;

public sealed record ConnectDiscordWorkspaceRequest(string GuildId, string Mode, string RelayPairingId);
public sealed record CommunicationConnectionResponse(
    Guid Id, Guid OrganizationId, string Provider, string WorkspaceExternalId,
    string WorkspaceMode, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record ProvisioningChangeResponse(
    string Change, string Kind, string Purpose, string DesiredName, string? ExternalId, string? Detail);
public sealed record ProvisioningPreviewResponse(Guid OrganizationId, string Provider, string WorkspaceExternalId,
    IReadOnlyList<ProvisioningChangeResponse> Changes, DateTimeOffset CreatedAt);
public sealed record LinkCodeResponse(string Code, DateTimeOffset ExpiresAt);
public sealed record RedeemExternalIdentityRequest(string GuildId, string ExternalUserId, string Code);
public sealed record ExternalIdentityLinkResponse(Guid Id, Guid OrganizationUserId, string ExternalUserId, bool IsVerified, Guid? ActiveDirectAgentOrganizationUserId);
public sealed record SelectDirectAgentRequest(Guid? AgentOrganizationUserId);
public sealed record CommunicationActionResponse(bool Succeeded, string? ErrorCode, string Message);
public sealed record NotificationResponse(Guid Id, Guid OrganizationId, Guid RecipientOrganizationUserId,
    Guid? OriginatingAgentOrganizationUserId, string Severity, string Category, string Title, string Body,
    string? ActionUri, DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, DateTimeOffset? DismissedAt);
