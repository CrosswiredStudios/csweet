using CSweet.Agent.Contracts.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace CSweet.AgentHost.Broker;

public sealed class AgentBrokerService : AgentBroker.AgentBrokerBase
{
    private readonly IAgentAuthorizationPolicy _authorizationPolicy;
    private readonly AgentSessionRegistry _sessions;
    private readonly ILogger<AgentBrokerService> _logger;

    public AgentBrokerService(
        IAgentAuthorizationPolicy authorizationPolicy,
        AgentSessionRegistry sessions,
        ILogger<AgentBrokerService> logger)
    {
        _authorizationPolicy = authorizationPolicy;
        _sessions = sessions;
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

        var session = _sessions.Register(firstMessage.Register, grant);
        var registration = new RegistrationResult
        {
            Accepted = true,
            SessionId = session.SessionId
        };
        registration.GrantedCapabilities.AddRange(grant.Capabilities);
        registration.GrantedSubscriptions.AddRange(grant.Subscriptions);
        registration.GrantedPublications.AddRange(grant.Publications);

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
                    _sessions.PublishEvent(
                        session,
                        message.PublishEvent,
                        message.CorrelationId);
                    break;

                case AgentToBrokerMessage.PayloadOneofCase.CapabilityRequest:
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
