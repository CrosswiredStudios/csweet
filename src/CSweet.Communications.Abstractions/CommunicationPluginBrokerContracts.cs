namespace CSweet.Communications.Abstractions;

public sealed record CommunicationPluginIngressRequest(
    Guid PluginInstallationId,
    Guid OrganizationId,
    NormalizedCommunicationEnvelope Envelope);

public sealed record CommunicationPluginLinkCodeRequest(string Code, DateTimeOffset ExpiresAt);
public sealed record CommunicationPluginIdentityRequest(
    string WorkspaceExternalId,
    string ExternalUserId,
    string MemberRoleExternalId);
