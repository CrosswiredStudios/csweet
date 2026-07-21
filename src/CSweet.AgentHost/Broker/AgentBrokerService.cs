using CSweet.Agent.Contracts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using CSweet.Application.Setup;
using CSweet.Application.Core;
using CSweet.Agent.SDK;
using System.Text.Json;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Google.Protobuf.WellKnownTypes;

namespace CSweet.AgentHost.Broker;

public sealed class AgentBrokerService : AgentBroker.AgentBrokerBase
{
    private readonly IAgentAuthorizationPolicy _authorizationPolicy;
    private readonly AgentSessionRegistry _sessions;
    private readonly ILogger<AgentBrokerService> _logger;
    private readonly IAgentRuntimeSignalService _runtimeSignals;
    private readonly IExecutiveBriefingService _executiveBriefings;
    private readonly IReadOnlyList<IPlatformCapabilityHandler> _platformCapabilities;
    private readonly IPlatformCapabilityDispatcher _platformDispatcher;
    private readonly IReadOnlyList<IPlatformEventObserver> _platformEventObservers;
    private readonly CSweetDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IAuditEventWriter _audit;
    private readonly IAuditExecutionContextAccessor _auditContext;

    public AgentBrokerService(
        IAgentAuthorizationPolicy authorizationPolicy,
        AgentSessionRegistry sessions,
        IAgentRuntimeSignalService runtimeSignals,
        IExecutiveBriefingService executiveBriefings,
        IEnumerable<IPlatformCapabilityHandler> platformCapabilities,
        IPlatformCapabilityDispatcher platformDispatcher,
        IEnumerable<IPlatformEventObserver> platformEventObservers,
        CSweetDbContext db,
        IConfiguration configuration,
        IAuditEventWriter audit,
        IAuditExecutionContextAccessor auditContext,
        ILogger<AgentBrokerService> logger)
    {
        _authorizationPolicy = authorizationPolicy;
        _sessions = sessions;
        _runtimeSignals = runtimeSignals;
        _executiveBriefings = executiveBriefings;
        _platformCapabilities = platformCapabilities.ToList();
        _platformDispatcher = platformDispatcher;
        _platformEventObservers = platformEventObservers.ToList();
        _db = db;
        _configuration = configuration;
        _audit = audit;
        _auditContext = auditContext;
        _logger = logger;
    }

    public override async Task Connect(
        IAsyncStreamReader<AgentToBrokerMessage> requestStream,
        IServerStreamWriter<BrokerToAgentMessage> responseStream,
        ServerCallContext context)
    {
        if (!await requestStream.MoveNext(context.CancellationToken))
        {
            await _audit.AppendAsync(new AuditEventWriteRequest(
                "broker.connection.empty", "BrokerConnection", "Inbound", "Rejected",
                Summary: "A broker connection closed before registration.",
                Actor: new AuditActor("Unknown", false, RemotePeer: context.Peer),
                ErrorCode: "registration_missing"), context.CancellationToken);
            return;
        }

        var firstMessage = requestStream.Current;
        if (firstMessage.PayloadCase != AgentToBrokerMessage.PayloadOneofCase.Register)
        {
            await _audit.AppendAsync(new AuditEventWriteRequest(
                "broker.registration.rejected", "BrokerConnection", "Inbound", "Rejected",
                ExternalMessageId: firstMessage.MessageId, CorrelationId: firstMessage.CorrelationId,
                Summary: "The first broker message was not a registration request.",
                Actor: new AuditActor("Unknown", false, RemotePeer: context.Peer),
                ErrorCode: "registration_required"), context.CancellationToken);
            await responseStream.WriteAsync(CreateRejectedRegistration(
                "The first message on an agent connection must be a registration request."));
            return;
        }

        var authorization = await _authorizationPolicy.AuthorizeAsync(
            firstMessage.Register,
            context.CancellationToken);
        if (!authorization.Accepted || authorization.Grant is null)
        {
            await _audit.AppendAsync(RegistrationAudit(firstMessage, context.Peer, "Rejected",
                authorization.RejectionReason, false), context.CancellationToken);
            await responseStream.WriteAsync(CreateRejectedRegistration(authorization.RejectionReason));
            return;
        }

        var grant = authorization.Grant;

        var session = _sessions.Register(firstMessage.Register, grant);
        Guid registrationAuditId;
        try
        {
            registrationAuditId = await _audit.AppendAsync(RegistrationAudit(firstMessage, context.Peer,
                "Accepted", "Agent registration accepted.", true, session), context.CancellationToken);
        }
        catch
        {
            _sessions.Unregister(session);
            throw;
        }

        if (!string.IsNullOrWhiteSpace(firstMessage.Register.RuntimeInstanceId))
        {
            try
            {
                await _runtimeSignals.RecordBrokerRegistrationAsync(
                    Guid.Parse(firstMessage.Register.RuntimeInstanceId),
                    Guid.Parse(firstMessage.Register.TickId),
                    Guid.Parse(firstMessage.Register.InstallationId),
                    firstMessage.Register.WorkloadToken,
                    context.CancellationToken);
                try
                {
                    await _executiveBriefings.QueueRuntimeStartupAsync(
                        Guid.Parse(firstMessage.Register.InstallationId),
                        Guid.Parse(firstMessage.Register.RuntimeInstanceId),
                        context.CancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogWarning(exception,
                        "The runtime registered successfully, but its startup executive briefing could not be queued.");
                }
            }
            catch (Exception exception) when (exception is FormatException or InvalidOperationException)
            {
                await _audit.AppendAsync(new AuditEventWriteRequest(
                    "broker.registration.runtime-rejected", "BrokerConnection", "Internal", "Rejected",
                    BrokerAuditIdentity.OrganizationId(session), "AgentSession", ParentEventId: registrationAuditId,
                    CorrelationId: firstMessage.CorrelationId, Actor: BrokerAuditIdentity.Actor(session, context.Peer),
                    ErrorCode: "invalid_runtime_context", ErrorMessage: exception.Message), context.CancellationToken);
                _sessions.Unregister(session);
                _logger.LogWarning(exception, "Rejected invalid runtime registration for installation {InstallationId}.", firstMessage.Register.InstallationId);
                await responseStream.WriteAsync(CreateRejectedRegistration("The runtime registration context is invalid."));
                return;
            }
        }

        var registration = new RegistrationResult
        {
            Accepted = true,
            SessionId = session.SessionId
        };
        registration.GrantedCapabilities.AddRange(grant.Capabilities);
        registration.GrantedSubscriptions.AddRange(grant.Subscriptions);
        registration.GrantedPublications.AddRange(grant.Publications);
        registration.GrantedPermissions.AddRange(grant.Permissions);
        registration.GrantedRequestedCapabilities.AddRange(
            (grant.RequestedCapabilities ?? new HashSet<string>(StringComparer.Ordinal))
            .Where(capability => !McpToolCatalog.IsGlobalCapability(capability)));
        registration.GlobalCapabilities.AddRange(McpToolCatalog.GlobalCapabilities.Order(StringComparer.Ordinal));
        registration.McpEndpoint = _configuration["Mcp:PublicEndpoint"] ?? DefaultMcpEndpoint(context.Host);
        registration.McpAccessToken = session.ConsumeInitialMcpAccessToken();
        registration.McpTokenExpiresAt = Timestamp.FromDateTimeOffset(session.McpTokenExpiresAt);
        if (Guid.TryParse(firstMessage.Register.InstallationId, out var registeredInstallationId))
            registration.GrantRevision = await _db.AgentInstallations.AsNoTracking()
                .Where(x => x.Id == registeredInstallationId)
                .Select(x => (long)x.RevisionNumber)
                .SingleOrDefaultAsync(context.CancellationToken);

        var registrationMessage = new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = firstMessage.CorrelationId,
            Registration = registration
        };
        await _audit.AppendAsync(new AuditEventWriteRequest(
            "broker.registration.response", "BrokerConnection", "Outbound", "Delivered",
            BrokerAuditIdentity.OrganizationId(session), "RegistrationResult",
            ParentEventId: registrationAuditId, ExternalMessageId: registrationMessage.MessageId,
            CorrelationId: firstMessage.CorrelationId, Actor: new AuditActor("Platform", DisplayName: "C-Sweet broker"),
            Target: BrokerAuditIdentity.Target(session)), context.CancellationToken);
        await responseStream.WriteAsync(registrationMessage);

        var readTask = ProcessInboundAsync(session, requestStream, context.CancellationToken);
        _ = readTask.ContinueWith(
            _ => session.Complete(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            await foreach (var outbound in session.Outbound.ReadAllAsync(context.CancellationToken))
            {
                await responseStream.WriteAsync(outbound);
            }
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _audit.AppendAsync(new AuditEventWriteRequest(
                "broker.connection.disconnected", "BrokerConnection", "Internal", "Completed",
                BrokerAuditIdentity.OrganizationId(session), "AgentSession",
                Summary: $"Agent session {session.SessionId} disconnected.",
                Actor: BrokerAuditIdentity.Actor(session, context.Peer)), CancellationToken.None);
            _sessions.Unregister(session);
        }

        try
        {
            await readTask;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task ProcessInboundAsync(
        AgentSession session,
        IAsyncStreamReader<AgentToBrokerMessage> requestStream,
        CancellationToken cancellationToken)
    {
        while (await requestStream.MoveNext(cancellationToken))
        {
            var message = requestStream.Current;
            var inboundAuditId = await AuditInboundAsync(session, message, cancellationToken);
            using var auditScope = _auditContext.Push(new AuditExecutionContext(
                BrokerAuditIdentity.OrganizationId(session), BrokerAuditIdentity.Actor(session), inboundAuditId,
                Guid.NewGuid()));

            switch (message.PayloadCase)
            {
                case AgentToBrokerMessage.PayloadOneofCase.PublishEvent:
                    if (session.Grant.Publications.Contains(message.PublishEvent.EventType))
                    {
                        foreach (var observer in _platformEventObservers.Where(x => x.CanObserve(message.PublishEvent.EventType)))
                        {
                            try { await observer.ObserveAsync(session, message.PublishEvent, cancellationToken); }
                            catch (JsonException exception)
                            {
                                _logger.LogWarning(exception, "Ignored malformed platform event {EventType} from {AgentId}.", message.PublishEvent.EventType, session.AgentId);
                            }
                        }
                    }
                    if (message.PublishEvent.EventType == "com.csweet.runtime.completed.v1" &&
                        session.Grant.Publications.Contains(message.PublishEvent.EventType))
                    {
                        await RecordCompletionAsync(session, message.PublishEvent, cancellationToken);
                    }
                    _sessions.PublishEvent(
                        session,
                        message.PublishEvent,
                        message.CorrelationId);
                    break;

                case AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest:
                    var platformHandler = _platformCapabilities.FirstOrDefault(x => x.CanHandle(message.CapabilityRequest.Capability));
                    if (platformHandler is not null)
                    {
                        await foreach (var result in _platformDispatcher.InvokeAsync(
                            session,
                            message.CapabilityRequest,
                            cancellationToken))
                        {
                            _sessions.SendAudited(session, new BrokerToAgentMessage
                            {
                                MessageId = Guid.NewGuid().ToString("N"),
                                CorrelationId = message.CorrelationId,
                                CapabilityResult = result
                            }, new AuditEventWriteRequest(
                                "broker.platform-capability.result", "BrokerCapability", "Outbound",
                                result.Succeeded ? "Delivered" : "Failed", BrokerAuditIdentity.OrganizationId(session),
                                "CapabilityResult", ParentEventId: inboundAuditId,
                                ExternalRequestId: result.RequestId, CorrelationId: message.CorrelationId,
                                Actor: new AuditActor("Platform", DisplayName: "C-Sweet platform"),
                                Target: BrokerAuditIdentity.Target(session), ContentType: result.ContentType,
                                Payload: result.Payload.ToByteArray(), ErrorMessage: result.Error));
                        }
                        break;
                    }
                    _sessions.RequestCapability(
                        session,
                        message.CapabilityRequest,
                        message.CorrelationId);
                    break;

                case AgentToBrokerMessage.PayloadOneofCase.CapabilityResult:
                    _sessions.CompleteCapability(
                        session,
                        message.CapabilityResult,
                        message.CorrelationId);
                    break;

                case AgentToBrokerMessage.PayloadOneofCase.Acknowledge:
                case AgentToBrokerMessage.PayloadOneofCase.Heartbeat:
                    break;

                case AgentToBrokerMessage.PayloadOneofCase.Register:
                    _sessions.SendAudited(session, new BrokerToAgentMessage
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        CorrelationId = message.CorrelationId,
                        Error = new BrokerError
                        {
                            Code = "duplicate_registration",
                            Message = "An agent session may register only once."
                        }
                    }, new AuditEventWriteRequest(
                        "broker.duplicate-registration.error", "BrokerConnection", "Outbound", "Denied",
                        BrokerAuditIdentity.OrganizationId(session), "BrokerError", ParentEventId: inboundAuditId,
                        CorrelationId: message.CorrelationId,
                        Actor: new AuditActor("Platform", DisplayName: "C-Sweet broker"),
                        Target: BrokerAuditIdentity.Target(session), ErrorCode: "duplicate_registration"));
                    break;

                default:
                    _logger.LogDebug(
                        "Ignored empty agent message {MessageId} from session {SessionId}.",
                        message.MessageId,
                        session.SessionId);
                    break;
            }
        }
    }

    private async Task<Guid> AuditInboundAsync(
        AgentSession session,
        AgentToBrokerMessage message,
        CancellationToken cancellationToken)
    {
        var payload = message.PayloadCase switch
        {
            AgentToBrokerMessage.PayloadOneofCase.PublishEvent => message.PublishEvent.Payload.ToByteArray(),
            AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest => message.CapabilityRequest.Payload.ToByteArray(),
            AgentToBrokerMessage.PayloadOneofCase.CapabilityResult => message.CapabilityResult.Payload.ToByteArray(),
            _ => null
        };
        var contentType = message.PayloadCase switch
        {
            AgentToBrokerMessage.PayloadOneofCase.PublishEvent => message.PublishEvent.ContentType,
            AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest => message.CapabilityRequest.ContentType,
            AgentToBrokerMessage.PayloadOneofCase.CapabilityResult => message.CapabilityResult.ContentType,
            _ => null
        };
        var requestId = message.PayloadCase switch
        {
            AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest => message.CapabilityRequest.RequestId,
            AgentToBrokerMessage.PayloadOneofCase.CapabilityResult => message.CapabilityResult.RequestId,
            _ => null
        };
        var category = message.PayloadCase is AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest or
            AgentToBrokerMessage.PayloadOneofCase.CapabilityResult ? "BrokerCapability" : "BrokerEvent";
        return await _audit.AppendAsync(new AuditEventWriteRequest(
            $"broker.message.{message.PayloadCase.ToString().ToLowerInvariant()}", category, "Inbound", "Received",
            BrokerAuditIdentity.OrganizationId(session), message.PayloadCase.ToString(),
            Summary: $"Received {message.PayloadCase} from {session.AgentId}.",
            ExternalMessageId: message.MessageId, ExternalRequestId: requestId,
            CorrelationId: message.CorrelationId, Actor: BrokerAuditIdentity.Actor(session),
            ContentType: contentType, Payload: payload), cancellationToken);
    }

    private static AuditEventWriteRequest RegistrationAudit(
        AgentToBrokerMessage message,
        string remotePeer,
        string outcome,
        string? summary,
        bool verified,
        AgentSession? session = null)
    {
        var registration = message.Register;
        var organizationId = Guid.TryParse(registration.BusinessId, out var businessId) ? businessId : (Guid?)null;
        var actor = session is null
            ? new AuditActor("Agent", verified, DisplayName: registration.AgentId, AgentId: registration.AgentId,
                InstallationId: Guid.TryParse(registration.InstallationId, out var installationId) ? installationId : null,
                RuntimeInstanceId: Guid.TryParse(registration.RuntimeInstanceId, out var runtimeId) ? runtimeId : null,
                TickId: Guid.TryParse(registration.TickId, out var tickId) ? tickId : null,
                PackageVersion: registration.AgentVersion, RemotePeer: remotePeer)
            : BrokerAuditIdentity.Actor(session, remotePeer) with { PackageVersion = registration.AgentVersion };
        return new AuditEventWriteRequest("broker.registration", "BrokerConnection", "Inbound", outcome,
            organizationId, "AgentSession", Summary: summary, ExternalMessageId: message.MessageId,
            CorrelationId: message.CorrelationId, Actor: actor,
            ErrorCode: outcome == "Rejected" ? "registration_rejected" : null);
    }

    private async Task RecordCompletionAsync(AgentSession session, PublishEvent publishedEvent, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(session.RuntimeInstanceId, out var runtimeId) ||
            !Guid.TryParse(session.TickId, out var tickId) ||
            !Guid.TryParse(session.InstallationId, out var installationId))
            throw new RpcException(new Status(StatusCode.PermissionDenied, "Completion publisher has no valid runtime context."));
        var payload = publishedEvent.Payload.ToStringUtf8();
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!TryReadGuid(root, "runtimeInstanceId", out var payloadRuntimeId) || payloadRuntimeId != runtimeId ||
                !TryReadGuid(root, "tickId", out var payloadTickId) || payloadTickId != tickId ||
                !TryReadGuid(root, "installationId", out var payloadInstallationId) || payloadInstallationId != installationId)
                throw new RpcException(new Status(StatusCode.PermissionDenied, "Completion payload identity does not match its publisher."));
        }
        catch (JsonException exception)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Completion payload must be valid JSON."), exception.Message);
        }
        await _runtimeSignals.RecordCompletionAsync(runtimeId, tickId, installationId, payload, cancellationToken);
    }

    private static bool TryReadGuid(JsonElement root, string propertyName, out Guid value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property) && Guid.TryParse(property.GetString(), out value);
    }

    private static BrokerToAgentMessage CreateRejectedRegistration(string reason) =>
        new()
        {
            MessageId = Guid.NewGuid().ToString("N"),
            Registration = new RegistrationResult
            {
                Accepted = false,
                RejectionReason = reason
            }
        };

    private static string DefaultMcpEndpoint(string grpcHost)
    {
        var grpc = new Uri($"http://{grpcHost}");
        return new UriBuilder(grpc) { Port = grpc.Port + 1, Path = "/mcp" }.Uri.ToString();
    }
}
