using System.Collections.Concurrent;
using CSweet.Agent.Contracts.Grpc;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;

namespace CSweet.AgentHost.Broker;

public sealed class AgentSessionRegistry
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();
    private readonly ConcurrentDictionary<string, PendingCapabilityRequest> _pendingCapabilities = new();
    private readonly ILogger<AgentSessionRegistry> _logger;

    public AgentSessionRegistry(ILogger<AgentSessionRegistry> logger)
    {
        _logger = logger;
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
            grant);

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
                pending.ProviderSessionId == session.SessionId &&
                _sessions.TryGetValue(pending.RequesterSessionId, out var requester))
            {
                SendCapabilityFailure(
                    requester,
                    pending.RequestId,
                    pending.CorrelationId,
                    "The selected capability provider disconnected before completing the request.");
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

        var deliveredCount = 0;
        foreach (var target in _sessions.Values)
        {
            if (!string.Equals(
                    target.BusinessId,
                    source.BusinessId,
                    StringComparison.Ordinal) ||
                !target.Grant.Subscriptions.Contains(publishedEvent.EventType))
            {
                continue;
            }

            if (target.TrySend(new BrokerToAgentMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = correlationId,
                Event = delivered
            }))
            {
                deliveredCount++;
            }
            else
            {
                _logger.LogWarning(
                    "Dropped event {EventType} for session {SessionId} because its bounded queue is full.",
                    publishedEvent.EventType,
                    target.SessionId);
            }
        }

        _logger.LogDebug(
            "Broker delivered event {EventType} from {AgentId} to {DeliveredCount} sessions.",
            publishedEvent.EventType,
            source.AgentId,
            deliveredCount);
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

        var provider = _sessions.Values
            .Where(session =>
                string.Equals(
                    session.BusinessId,
                    requester.BusinessId,
                    StringComparison.Ordinal) &&
                session.Grant.Capabilities.Contains(request.Capability))
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();

        if (provider is null)
        {
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                $"No authorized agent currently provides '{request.Capability}'.");
            return;
        }

        var pending = new PendingCapabilityRequest(
            request.RequestId,
            requester.SessionId,
            provider.SessionId,
            correlationId);

        if (!_pendingCapabilities.TryAdd(request.RequestId, pending))
        {
            SendCapabilityFailure(
                requester,
                request.RequestId,
                correlationId,
                "A capability request with this id is already pending.");
            return;
        }

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
        if (!_pendingCapabilities.TryRemove(result.RequestId, out var pending))
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

        if (_sessions.TryGetValue(pending.RequesterSessionId, out var requester))
        {
            requester.TrySend(new BrokerToAgentMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                CorrelationId = pending.CorrelationId,
                CapabilityResult = result
            });
        }
    }

    private static void SendCapabilityFailure(
        AgentSession requester,
        string requestId,
        string correlationId,
        string error)
    {
        requester.TrySend(new BrokerToAgentMessage
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
        });
    }

    private static void SendError(
        AgentSession session,
        string correlationId,
        string code,
        string message)
    {
        session.TrySend(new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            Error = new BrokerError
            {
                Code = code,
                Message = message
            }
        });
    }

    private sealed record PendingCapabilityRequest(
        string RequestId,
        string RequesterSessionId,
        string ProviderSessionId,
        string CorrelationId);
}
