namespace CSweet.Communications.Abstractions;

public enum CommunicationResourceKind { Category, Channel, Role, Webhook, Command, Thread }
public enum CommunicationChangeKind { Create, Update, Archive, Restore, NoChange, Blocked }
public enum CommunicationEnvelopeKind { Message, Interaction, ResourceChanged, ConnectionChanged, Acknowledgement }

public sealed record CommunicationProviderHealth(
    string Provider, bool IsAvailable, string Status, DateTimeOffset CheckedAt, string? Detail = null);

public sealed record CommunicationRateLimit(
    DateTimeOffset? RetryAt = null, string? Bucket = null, bool IsGlobal = false);

public sealed record CommunicationError(
    string Code, string Message, bool IsTransient = false, CommunicationRateLimit? RateLimit = null);

public sealed record CommunicationResult(bool Succeeded, CommunicationError? Error = null, string? ExternalId = null)
{
    public static CommunicationResult Success(string? externalId = null) => new(true, ExternalId: externalId);
    public static CommunicationResult Failure(string code, string message, bool transient = false, CommunicationRateLimit? rateLimit = null) =>
        new(false, new CommunicationError(code, message, transient, rateLimit));
}

public sealed record ProviderResourceDescriptor(
    string Provider, CommunicationResourceKind Kind, string ExternalId, string Name,
    string Purpose, string? ParentExternalId = null, IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record WorkspaceProvisioningChange(
    CommunicationChangeKind Change, CommunicationResourceKind Kind, string Purpose,
    string DesiredName, string? ExternalId = null, string? Detail = null);

public sealed record WorkspaceProvisioningPlan(
    Guid OrganizationId, string Provider, string WorkspaceExternalId,
    IReadOnlyList<WorkspaceProvisioningChange> Changes, DateTimeOffset CreatedAt);

public sealed record WorkspaceProvisioningResult(
    bool Succeeded, IReadOnlyList<ProviderResourceDescriptor> Resources,
    IReadOnlyList<CommunicationError> Errors);

public sealed record NormalizedCommunicationEnvelope(
    Guid Id, string Provider, CommunicationEnvelopeKind Kind, string WorkspaceExternalId,
    string? ChannelExternalId, string? ThreadExternalId, string? SenderExternalId,
    string? MessageExternalId, string? ReplyToExternalId, string? Content,
    IReadOnlyList<string> MentionExternalIds, bool IsBot, bool IsWebhook,
    DateTimeOffset OccurredAt, string IdempotencyKey,
    IReadOnlyDictionary<string, string>? Metadata = null);

public sealed record OutboundCommunicationEnvelope(
    Guid Id, string Provider, string WorkspaceExternalId, string DestinationExternalId,
    string Content, string? ThreadExternalId, string? ReplyToExternalId,
    string? PersonaName, string? PersonaAvatarUrl, string IdempotencyKey,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface ICommunicationProvider
{
    string Provider { get; }
    Task<CommunicationProviderHealth> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<CommunicationResult> SendAsync(OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default);
}

public interface IWorkspaceProvisioner
{
    string Provider { get; }
    Task<WorkspaceProvisioningPlan> PlanAsync(Guid organizationId, string workspaceExternalId, CancellationToken cancellationToken = default);
    Task<WorkspaceProvisioningResult> ApplyAsync(WorkspaceProvisioningPlan plan, CancellationToken cancellationToken = default);
}

public interface IWorkspaceReconciler
{
    string Provider { get; }
    Task<WorkspaceProvisioningResult> ReconcileAsync(Guid organizationId, string workspaceExternalId, CancellationToken cancellationToken = default);
}

public interface IExternalIdentityProvider
{
    string Provider { get; }
    Task<CommunicationResult> AssignMemberAsync(string workspaceExternalId, string externalUserId, string memberRoleExternalId, CancellationToken cancellationToken = default);
}

public interface ICommunicationRelayClient
{
    IAsyncEnumerable<NormalizedCommunicationEnvelope> ReadInboundAsync(Guid pairingId, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid pairingId, Guid envelopeId, CancellationToken cancellationToken = default);
    Task<CommunicationResult> SendAsync(Guid pairingId, OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default);
    Task<WorkspaceProvisioningResult> ApplyProvisioningAsync(Guid pairingId, WorkspaceProvisioningPlan plan, CancellationToken cancellationToken = default);
    Task RegisterLinkCodeAsync(Guid pairingId, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
    Task<CommunicationResult> AssignMemberAsync(Guid pairingId, string workspaceExternalId, string externalUserId,
        string memberRoleExternalId, CancellationToken cancellationToken = default);
}
