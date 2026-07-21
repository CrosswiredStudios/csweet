using System.Collections.Concurrent;
using CSweet.Agent.Contracts.Grpc;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using CSweet.Application.Setup;

namespace CSweet.AgentHost.Broker;

public sealed class AgentSessionRegistry
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly ConcurrentDictionary<string, PendingCapabilityRequest> _pendingCapabilities = new();
    private readonly ILogger<AgentSessionRegistry> _logger;
    private readonly IAuditEventWriter? _audit;

    public AgentSessionRegistry(ILogger<AgentSessionRegistry> logger, IAuditEventWriter? audit = null)
    {
        _logger = logger;
        _audit = audit;
    }

    public AgentSession Register(
        RegisterAgent registration,
        AuthorizedAgentGrant grant)
    {
        var session = new AgentSession(
            Guid.NewGuid().ToString("N"),
            registration.AgentId,
            registration.InstallationId,
            registration.BusinessId,
            registration.RuntimeInstanceId,
            registration.TickId,
            registration.WorkloadToken,
            grant,
            registration.AgentVersion);

        if (!_sessions.TryAdd(session.SessionId, session))
        {
            throw new InvalidOperationException("Unable to allocate an agent session.");
        }

        _logger.LogInformation(
            "Registered agent {AgentId} installation {InstallationId} for business {BusinessId} as session {SessionId}.",
            session.AgentId,
            session.InstallationId,
            session.BusinessId,
            session.SessionId);

        return session;
    }

    public void Unregister(AgentSession session)
    {
        if (!_sessions.TryRemove(session.SessionId, out _))
        {
            return;
        }

        session.Complete();

        foreach (var pair in _pendingCapabilities.ToArray())
        {
            var pending = pair.Value;
            if (pending.RequesterSessionId != session.SessionId &&
                pending.ProviderSessionId != session.SessionId)
            {
                continue;
            }

            if (_pendingCapabilities.TryRemove(pair.Key, out _) &&
                pending.ProviderSessionId == session.SessionId)
            {
                const string error = "The selected capability provider disconnected before completing the request.";
                if (pending.PlatformCompletion is not null)
                    pending.PlatformCompletion.TrySetResult(CapabilityFailure(pending.RequestId, error));
                else if (pending.RequesterSessionId is not null &&
                         _sessions.TryGetValue(pending.RequesterSessionId, out var requester))
                    SendCapabilityFailure(requester, pending.RequestId, pending.CorrelationId, error);
            }
        }

        _logger.LogInformation(
            "Unregistered agent session {SessionId} for {AgentId}.",
            session.SessionId,
            session.AgentId);
    }

    public void PublishEvent(
        AgentSession source,
        PublishEvent publishedEvent,
        string correlationId)
    {
        if (!source.Grant.Publications.Contains(publishedEvent.EventType))
        {
            SendError(
                source,
                correlationId,
                "publication_denied",
                $"Agent '{source.AgentId}' may not publish '{publishedEvent.EventType}'.");
            return;
        }

        var delivered = new DeliveredEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            SourceAgentId = source.AgentId,
            EventType = publishedEvent.EventType,
            SchemaVersion = publishedEvent.SchemaVersion,
            Subject = publishedEvent.Subject,
            ContentType = publishedEvent.ContentType,
            Payload = publishedEvent.Payload,
            OccurredAt = Timestamp.FromDateTime(DateTime.UtcNow)
        };

        var targetInstallationId = GetTargetInstallationId(publishedEvent.Subject);
        var mayRouteInstallation = targetInstallationId is not null && source.Grant.Permissions.Contains("installation.route");
        var deliveredCount = 0;
        var deliveredTargets = new List<string>();
        foreach (var target in _sessions.Values)
        {
            if ((!string.Equals(
                    target.BusinessId,
                    source.BusinessId,
                    StringComparison.Ordinal) && !mayRouteInstallation) ||
                (targetInstallationId is not null &&
                    !string.Equals(target.InstallationId, targetInstallationId, StringComparison.OrdinalIgnoreCase)) ||
                !target.Grant.Subscriptions.Contains(publishedEvent.EventType))
            {
                continue;
            }

            var deliveryId = Append(new AuditEventWriteRequest(
                "broker.event.delivery", "BrokerEvent", "Outbound", "Authorized",
                BrokerAuditIdentity.OrganizationId(source), "DeliveredEvent",
                Summary: $"Authorized delivery of {publishedEvent.EventType} to {target.AgentId}.",
                CorrelationId: correlationId, Actor: BrokerAuditIdentity.Actor(source),
                Target: BrokerAuditIdentity.Target(target), ContentType: publishedEvent.ContentType,
                Payload: publishedEvent.Payload.ToByteArray()));
            var sent = target.TrySend(new BrokerToAgentMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                Event = delivered
            });
            Append(new AuditEventWriteRequest(
                "broker.event.delivery.result", "BrokerEvent", "Outbound", sent ? "Delivered" : "Dropped",
                BrokerAuditIdentity.OrganizationId(source), "DeliveredEvent",
                Summary: sent ? $"Enqueued {publishedEvent.EventType} for {target.AgentId}."
                    : $"Dropped {publishedEvent.EventType} for {target.AgentId} because its queue was full.",
                ParentEventId: deliveryId, CorrelationId: correlationId, Actor: BrokerAuditIdentity.Actor(source),
                Target: BrokerAuditIdentity.Target(target), ErrorCode: sent ? null : "target_queue_full"));
            if (sent)
            {
                deliveredCount++;
                deliveredTargets.Add(target.AgentId);
            }
            else
            {
                _logger.LogWarning(
                    "Dropped event {EventType} for session {SessionId} because its bounded queue is full.",
                    publishedEvent.EventType,
                    target.SessionId);
            }
        }

        if (deliveredCount == 0)
        {
            var error = $"No connected agent installation is subscribed to '{publishedEvent.EventType}' for subject '{publishedEvent.Subject}'.";
            _logger.LogWarning(
                "Broker delivered event {EventType} from {AgentId} on subject {Subject} to 0 sessions in business {BusinessId}. Correlation {CorrelationId}. Check subscriber registration and grants.",
                publishedEvent.EventType,
                source.AgentId,
                publishedEvent.Subject,
                source.BusinessId,
                correlationId);
            SendError(source, correlationId, "event_undelivered", error);
            return;
        }

        _logger.LogInformation(
            "Broker delivered event {EventType} from {AgentId} on subject {Subject} to {DeliveredCount} sessions ({Targets}). Correlation {CorrelationId}.",
            publishedEvent.EventType,
            source.AgentId,
            publishedEvent.Subject,
            deliveredCount,
            string.Join(", ", deliveredTargets),
            correlationId);
    }

    public AgentSession? FindByWorkloadToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return _sessions.Values.FirstOrDefault(session => session.MatchesWorkloadToken(token));
    }

    public AgentSession? FindByMcpAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        return _sessions.Values.FirstOrDefault(session => session.MatchesMcpAccessToken(token));
    }

    public bool SendAudited(AgentSession target, BrokerToAgentMessage message, AuditEventWriteRequest auditEvent)
    {
        var auditId = Append(auditEvent);
        var sent = target.TrySend(message);
        Append(auditEvent with
        {
            EventType = auditEvent.EventType + ".result",
            Outcome = sent ? "Delivered" : "Dropped",
            ParentEventId = auditId,
            Payload = null,
            ErrorCode = sent ? null : "target_queue_full"
        });
        return sent;
    }

    /// <summary>Publishes a trusted, durable application event without impersonating an agent.</summary>
    public int PublishPlatformEvent(string businessId, string eventType, string subject, ByteString payload, string correlationId,
        string? targetInstallationId = null, string? eventId = null, bool requireSubscription = true,
        DateTimeOffset? occurredAt = null)
    {
        var organizationId = Guid.TryParse(businessId, out var parsedOrganizationId) ? parsedOrganizationId : (Guid?)null;
        var platformEventAuditId = Append(new AuditEventWriteRequest(
            "broker.platform-event.publish", "BrokerEvent", "Internal", "Accepted", organizationId,
            "DeliveredEvent", Summary: $"Platform published {eventType} for business {businessId}.",
            ExternalMessageId: eventId, CorrelationId: correlationId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
            ContentType: "application/json", Payload: payload.ToByteArray()));
        var delivered = new DeliveredEvent
        {
            EventId = eventId ?? Guid.NewGuid().ToString("N"),
            SourceAgentId = "platform.csweet",
            EventType = eventType,
            SchemaVersion = "1.0",
            Subject = subject,
            ContentType = "application/json",
            Payload = payload,
            OccurredAt = Timestamp.FromDateTime((occurredAt ?? DateTimeOffset.UtcNow).UtcDateTime)
        };
        var count = 0;
        foreach (var target in _sessions.Values.Where(x =>
                     string.Equals(x.BusinessId, businessId, StringComparison.Ordinal) &&
                     (targetInstallationId is null || string.Equals(x.InstallationId, targetInstallationId, StringComparison.OrdinalIgnoreCase)) &&
                     (!requireSubscription || x.Grant.Subscriptions.Contains(eventType))))
        {
            var deliveryId = Append(new AuditEventWriteRequest(
                "broker.platform-event.delivery", "BrokerEvent", "Outbound", "Authorized", organizationId,
                "DeliveredEvent", Summary: $"Authorized platform event {eventType} for {target.AgentId}.",
                ParentEventId: platformEventAuditId, CorrelationId: correlationId,
                Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
                Target: BrokerAuditIdentity.Target(target), ContentType: "application/json", Payload: payload.ToByteArray()));
            var sent = target.TrySend(new BrokerToAgentMessage
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    CorrelationId = correlationId,
                    Event = delivered
                });
            Append(new AuditEventWriteRequest(
                "broker.platform-event.delivery.result", "BrokerEvent", "Outbound", sent ? "Delivered" : "Dropped",
                organizationId, "DeliveredEvent", ParentEventId: deliveryId, CorrelationId: correlationId,
                Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
                Target: BrokerAuditIdentity.Target(target), ErrorCode: sent ? null : "target_queue_full"));
            if (sent)
            {
                count++;
            }
        }
        Append(new AuditEventWriteRequest(
            "broker.platform-event.publish.result", "BrokerEvent", "Internal", count > 0 ? "Delivered" : "Undelivered",
            organizationId, "DeliveredEvent", Summary: $"Platform event {eventType} reached {count} session(s).",
            ParentEventId: platformEventAuditId, CorrelationId: correlationId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
            ErrorCode: count > 0 ? null : "event_undelivered"));
        return count;
    }

    /// <summary>Invokes a capability on an exact installation as the trusted platform.</summary>
    public async Task<CapabilityResult> InvokeInstallationCapabilityAsync(
        string businessId,
        string installationId,
        RequestCapability request,
        CancellationToken cancellationToken)
    {
        var platformOrganizationId = Guid.TryParse(businessId, out var parsedBusinessId) ? parsedBusinessId : (Guid?)null;
        var requestAuditId = await AppendAsync(new AuditEventWriteRequest(
            "broker.platform-capability.request", "BrokerCapability", "Internal", "Received",
            platformOrganizationId, "CapabilityRequest", ExternalRequestId: request.RequestId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
            Target: new AuditTarget("Agent", InstallationId: Guid.TryParse(installationId, out var targetId) ? targetId : null),
            ContentType: request.ContentType, Payload: request.Payload.ToByteArray()), cancellationToken);
        var provider = _sessions.Values
            .Where(session =>
                string.Equals(session.BusinessId, businessId, StringComparison.Ordinal) &&
                string.Equals(session.InstallationId, installationId, StringComparison.OrdinalIgnoreCase) &&
                session.Grant.Capabilities.Contains(request.Capability))
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (provider is null)
        {
            await AppendAsync(new AuditEventWriteRequest(
                "broker.platform-capability.request.result", "BrokerCapability", "Internal", "Failed",
                platformOrganizationId, "CapabilityRequest", ParentEventId: requestAuditId,
                ExternalRequestId: request.RequestId, Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
                ErrorCode: "provider_unavailable"), cancellationToken);
            return CapabilityFailure(
                request.RequestId,
                $"The target installation is not connected or does not provide '{request.Capability}'.");
        }

        var completion = new TaskCompletionSource<CapabilityResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var correlationId = Guid.NewGuid().ToString("N");
        var pending = new PendingCapabilityRequest(
            request.RequestId,
            null,
            provider.SessionId,
            correlationId,
            completion);
        if (!_pendingCapabilities.TryAdd(request.RequestId, pending))
            return CapabilityFailure(request.RequestId, "A capability request with this id is already pending.");

        var platformDeliveryId = Append(new AuditEventWriteRequest(
            "broker.platform-capability.delivery", "BrokerCapability", "Outbound", "Authorized",
            platformOrganizationId, "CapabilityRequest", ExternalRequestId: request.RequestId,
            ParentEventId: requestAuditId, CorrelationId: correlationId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
            Target: BrokerAuditIdentity.Target(provider), ContentType: request.ContentType,
            Payload: request.Payload.ToByteArray()));
        var accepted = provider.TrySend(new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            CapabilityRequest = new CapabilityRequest
            {
                RequestId = request.RequestId,
                RequestingAgentId = "platform.csweet",
                Capability = request.Capability,
                ContentType = request.ContentType,
                Payload = request.Payload
            }
        });
        Append(new AuditEventWriteRequest(
            "broker.platform-capability.delivery.result", "BrokerCapability", "Outbound",
            accepted ? "Delivered" : "Dropped", platformOrganizationId, "CapabilityRequest",
            ParentEventId: platformDeliveryId, ExternalRequestId: request.RequestId,
            CorrelationId: correlationId, Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
            Target: BrokerAuditIdentity.Target(provider), ErrorCode: accepted ? null : "provider_overloaded"));
        if (!accepted)
        {
            _pendingCapabilities.TryRemove(request.RequestId, out _);
            return CapabilityFailure(request.RequestId, "The target installation is currently overloaded.");
        }

        try
        {
            return await completion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _pendingCapabilities.TryRemove(request.RequestId, out _);
        }
    }

    public void RequestCapability(
        AgentSession requester,
        RequestCapability request,
        string correlationId)
    {
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.Capability))
        {
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                "Capability requests require both a request id and capability name.");
            return;
        }

        var targetInstallationId = GetInstallationSelector(request.TargetAgentId);
        var mayRouteInstallation = targetInstallationId is not null && requester.Grant.Permissions.Contains("installation.route");
        var provider = _sessions.Values
            .Where(session =>
                (string.Equals(
                    session.BusinessId,
                    requester.BusinessId,
                    StringComparison.Ordinal) || mayRouteInstallation) &&
                (string.IsNullOrWhiteSpace(request.TargetAgentId) ||
                    (targetInstallationId is not null && string.Equals(
                        session.InstallationId,
                        targetInstallationId,
                        StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(
                        session.AgentId,
                        request.TargetAgentId,
                        StringComparison.Ordinal)) &&
                session.Grant.Capabilities.Contains(request.Capability))
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (provider is null)
        {
            var installationSessions = targetInstallationId is null
                ? new List<string>()
                : _sessions.Values
                    .Where(session => string.Equals(session.InstallationId, targetInstallationId, StringComparison.OrdinalIgnoreCase))
                    .Select(session => $"{session.AgentId}/{session.InstallationId} business={session.BusinessId} capability={session.Grant.Capabilities.Contains(request.Capability)}")
                    .ToList();
            _logger.LogWarning(
                "No provider selected for capability {Capability}, target {Target}, requester {RequesterAgentId} in business {RequesterBusinessId}. InstallationRouteAllowed {InstallationRouteAllowed}. Matching installation sessions: {@InstallationSessions}",
                request.Capability,
                request.TargetAgentId,
                requester.AgentId,
                requester.BusinessId,
                mayRouteInstallation,
                installationSessions);
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                string.IsNullOrWhiteSpace(request.TargetAgentId)
                    ? $"No authorized agent currently provides '{request.Capability}'."
                    : $"No authorized agent '{request.TargetAgentId}' currently provides '{request.Capability}'.");
            return;
        }

        var requestedCapabilities = requester.Grant.RequestedCapabilities ?? new HashSet<string>(StringComparer.Ordinal);
        var legacyCapabilityGrant = requestedCapabilities.Count == 0 && requester.Grant.Permissions.Contains("capability.request");
        if (!legacyCapabilityGrant && !requestedCapabilities.Contains(request.Capability))
        {
            SendCapabilityFailure(requester, request.RequestId, correlationId,
                $"Agent '{requester.AgentId}' may not request '{request.Capability}'.");
            return;
        }

        var pending = new PendingCapabilityRequest(
            request.RequestId,
            requester.SessionId,
            provider.SessionId,
            correlationId,
            null);

        if (!_pendingCapabilities.TryAdd(request.RequestId, pending))
        {
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                "A capability request with this id is already pending.");
            return;
        }

        var deliveryId = Append(new AuditEventWriteRequest(
            "broker.capability.delivery", "BrokerCapability", "Outbound", "Authorized",
            BrokerAuditIdentity.OrganizationId(requester), "CapabilityRequest",
            Summary: $"Authorized capability {request.Capability} for provider {provider.AgentId}.",
            ExternalRequestId: request.RequestId, CorrelationId: correlationId,
            Actor: BrokerAuditIdentity.Actor(requester), Target: BrokerAuditIdentity.Target(provider),
            ContentType: request.ContentType, Payload: request.Payload.ToByteArray()));
        var accepted = provider.TrySend(new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            CapabilityRequest = new CapabilityRequest
            {
                RequestId = request.RequestId,
                RequestingAgentId = requester.AgentId,
                Capability = request.Capability,
                ContentType = request.ContentType,
                Payload = request.Payload
            }
        });
        Append(new AuditEventWriteRequest(
            "broker.capability.delivery.result", "BrokerCapability", "Outbound", accepted ? "Delivered" : "Dropped",
            BrokerAuditIdentity.OrganizationId(requester), "CapabilityRequest", ParentEventId: deliveryId,
            ExternalRequestId: request.RequestId, CorrelationId: correlationId,
            Actor: BrokerAuditIdentity.Actor(requester), Target: BrokerAuditIdentity.Target(provider),
            ErrorCode: accepted ? null : "provider_overloaded"));

        if (!accepted)
        {
            _pendingCapabilities.TryRemove(request.RequestId, out _);
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                "The selected capability provider is currently overloaded.");
        }
    }

    public void CompleteCapability(
        AgentSession provider,
        CapabilityResult result,
        string correlationId)
    {
        if (!_pendingCapabilities.TryGetValue(result.RequestId, out var pending))
        {
            SendError(
                provider,
                correlationId,
                "unknown_capability_request",
                $"Capability request '{result.RequestId}' is not pending.");
            return;
        }

        if (!string.Equals(
                pending.ProviderSessionId,
                provider.SessionId,
                StringComparison.Ordinal))
        {
            SendError(
                provider,
                correlationId,
                "capability_result_denied",
                "This session was not selected to provide the capability result.");
            return;
        }

        if (!result.HasMore && !_pendingCapabilities.TryRemove(result.RequestId, out _))
        {
            SendError(
                provider,
                correlationId,
                "capability_result_already_completed",
                "The capability request was already completed.");
            return;
        }

        if (pending.PlatformCompletion is not null)
        {
            if (!result.HasMore)
                pending.PlatformCompletion.TrySetResult(result);
        }
        else if (pending.RequesterSessionId is not null &&
                 _sessions.TryGetValue(pending.RequesterSessionId, out var requester))
        {
            var deliveryId = Append(new AuditEventWriteRequest(
                "broker.capability-result.delivery", "BrokerCapability", "Outbound", "Authorized",
                BrokerAuditIdentity.OrganizationId(provider), "CapabilityResult",
                ExternalRequestId: result.RequestId, CorrelationId: pending.CorrelationId,
                Actor: BrokerAuditIdentity.Actor(provider), Target: BrokerAuditIdentity.Target(requester),
                ContentType: result.ContentType, Payload: result.Payload.ToByteArray(), ErrorMessage: result.Error));
            var sent = requester.TrySend(new BrokerToAgentMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = pending.CorrelationId,
                CapabilityResult = result
            });
            Append(new AuditEventWriteRequest(
                "broker.capability-result.delivery.result", "BrokerCapability", "Outbound",
                sent ? "Delivered" : "Dropped", BrokerAuditIdentity.OrganizationId(provider), "CapabilityResult",
                ParentEventId: deliveryId, ExternalRequestId: result.RequestId,
                CorrelationId: pending.CorrelationId, Actor: BrokerAuditIdentity.Actor(provider),
                Target: BrokerAuditIdentity.Target(requester), ErrorCode: sent ? null : "requester_queue_full"));
        }
    }

    private static string? GetInstallationSelector(string? target) =>
        target?.StartsWith("installation:", StringComparison.OrdinalIgnoreCase) == true
            ? target["installation:".Length..]
            : null;

    private static string? GetTargetInstallationId(string? subject)
    {
        const string prefix = "agent-installation/";
        if (subject?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        var remainder = subject[prefix.Length..];
        var separator = remainder.IndexOf('/');
        return separator < 0 ? remainder : remainder[..separator];
    }

    private void SendCapabilityFailure(
        AgentSession requester,
        string requestId,
        string correlationId,
        string error)
    {
        SendAudited(requester, new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            CapabilityResult = new CapabilityResult
            {
                RequestId = requestId,
                Succeeded = false,
                ContentType = "application/json",
                Error = error
            }
        }, new AuditEventWriteRequest(
            "broker.capability.error", "BrokerCapability", "Outbound", "Denied",
            BrokerAuditIdentity.OrganizationId(requester), "CapabilityResult", ExternalRequestId: requestId,
            CorrelationId: correlationId, Actor: new AuditActor("Platform", DisplayName: "C-Sweet broker"),
            Target: BrokerAuditIdentity.Target(requester), ErrorCode: "capability_denied", ErrorMessage: error));
    }

    private static CapabilityResult CapabilityFailure(string requestId, string error) => new()
    {
        RequestId = requestId,
        Succeeded = false,
        ContentType = "application/json",
        Error = error
    };

    private void SendError(
        AgentSession session,
        string correlationId,
        string code,
        string message)
    {
        SendAudited(session, new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            Error = new BrokerError
            {
                Code = code,
                Message = message
            }
        }, new AuditEventWriteRequest(
            "broker.error", "BrokerConnection", "Outbound", "Denied",
            BrokerAuditIdentity.OrganizationId(session), "BrokerError", CorrelationId: correlationId,
            Actor: new AuditActor("Platform", DisplayName: "C-Sweet broker"),
            Target: BrokerAuditIdentity.Target(session), ErrorCode: code, ErrorMessage: message));
    }

    private Guid Append(AuditEventWriteRequest request)
    {
        if (_audit is null) return Guid.NewGuid();
        return _audit.AppendAsync(request).GetAwaiter().GetResult();
    }

    private Task<Guid> AppendAsync(AuditEventWriteRequest request, CancellationToken cancellationToken)
    {
        if (_audit is null) return Task.FromResult(Guid.NewGuid());
        return _audit.AppendAsync(request, cancellationToken);
    }

    private sealed record PendingCapabilityRequest(
        string RequestId,
        string? RequesterSessionId,
        string ProviderSessionId,
        string CorrelationId,
        TaskCompletionSource<CapabilityResult>? PlatformCompletion);
}
