using CSweet.Agent.Contracts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using CSweet.Application.Setup;
using CSweet.Agent.SDK;
using System.Text.Json;

namespace CSweet.AgentHost.Broker;

public sealed class AgentBrokerService : AgentBroker.AgentBrokerBase
{
    private readonly IAgentAuthorizationPolicy _authorizationPolicy;
    private readonly AgentSessionRegistry _sessions;
    private readonly ILogger<AgentBrokerService> _logger;
    private readonly IAgentRuntimeSignalService _runtimeSignals;
    private readonly PlatformLlmCapabilityHandler _platformLlm;

    public AgentBrokerService(
        IAgentAuthorizationPolicy authorizationPolicy,
        AgentSessionRegistry sessions,
        IAgentRuntimeSignalService runtimeSignals,
        PlatformLlmCapabilityHandler platformLlm,
        ILogger<AgentBrokerService> logger)
    {
        _authorizationPolicy = authorizationPolicy;
        _sessions = sessions;
        _runtimeSignals = runtimeSignals;
        _platformLlm = platformLlm;
        _logger = logger;
    }

    public override async Task Connect(
        IAsyncStreamReader<AgentToBrokerMessage> requestStream,
        IServerStreamWriter<BrokerToAgentMessage> responseStream,
        ServerCallContext context)
    {
        if (!await requestStream.MoveNext(context.CancellationToken))
        {
            return;
        }

        var firstMessage = requestStream.Current;
        if (firstMessage.PayloadCase != AgentToBrokerMessage.PayloadOneofCase.Register)
        {
            await responseStream.WriteAsync(CreateRejectedRegistration(
                "The first message on an agent connection must be a registration request."));
            return;
        }

        var authorization = await _authorizationPolicy.AuthorizeAsync(
            firstMessage.Register,
            context.CancellationToken);
        if (!authorization.Accepted || authorization.Grant is null)
        {
            await responseStream.WriteAsync(CreateRejectedRegistration(authorization.RejectionReason));
            return;
        }

        var grant = authorization.Grant;

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
            }
            catch (Exception exception) when (exception is FormatException or InvalidOperationException)
            {
                _logger.LogWarning(exception, "Rejected invalid runtime registration for installation {InstallationId}.", firstMessage.Register.InstallationId);
                await responseStream.WriteAsync(CreateRejectedRegistration("The runtime registration context is invalid."));
                return;
            }
        }

        var session = _sessions.Register(firstMessage.Register, grant);
        var registration = new RegistrationResult
        {
            Accepted = true,
            SessionId = session.SessionId
        };
        registration.GrantedCapabilities.AddRange(grant.Capabilities);
        registration.GrantedSubscriptions.AddRange(grant.Subscriptions);
        registration.GrantedPublications.AddRange(grant.Publications);
        registration.GrantedPermissions.AddRange(grant.Permissions);

        await responseStream.WriteAsync(new BrokerToAgentMessage
        {
            MessageId = Guid.NewGuid().ToString("N"),
            CorrelationId = firstMessage.CorrelationId,
            Registration = registration
        });

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

            switch (message.PayloadCase)
            {
                case AgentToBrokerMessage.PayloadOneofCase.PublishEvent:
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
                    if (message.CapabilityRequest.Capability == BrokerLlmCapabilities.ChatStream)
                    {
                        await foreach (var result in _platformLlm.StreamAsync(
                            session,
                            message.CapabilityRequest,
                            cancellationToken))
                        {
                            session.TrySend(new BrokerToAgentMessage
                            {
                                MessageId = Guid.NewGuid().ToString("N"),
                                CorrelationId = message.CorrelationId,
                                CapabilityResult = result
                            });
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
                    session.TrySend(new BrokerToAgentMessage
                    {
                        MessageId = Guid.NewGuid().ToString("N"),
                        CorrelationId = message.CorrelationId,
                        Error = new BrokerError
                        {
                            Code = "duplicate_registration",
                            Message = "An agent session may register only once."
                        }
                    });
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
}
