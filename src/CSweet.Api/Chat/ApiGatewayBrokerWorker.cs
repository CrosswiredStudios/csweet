using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Contracts.Communications;
using Microsoft.Extensions.Options;

namespace CSweet.Api.Chat;

public sealed class ApiGatewayBrokerWorker : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly ApiGatewayBrokerConnection _connection;
    private readonly IChatStreamRouter _router;
    private readonly ApiGatewayOptions _options;
    private readonly ILogger<ApiGatewayBrokerWorker> _logger;

    public ApiGatewayBrokerWorker(
        IServiceProvider serviceProvider,
        ApiGatewayBrokerConnection connection,
        IChatStreamRouter router,
        IOptions<ApiGatewayOptions> options,
        ILogger<ApiGatewayBrokerWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _connection = connection;
        _router = router;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var broker = _serviceProvider.GetRequiredService<GrpcAgentBrokerClient>();

            try
            {
                await broker.StartAsync(CreateRegistration(), stoppingToken);
                _connection.Attach(broker);

                _logger.LogInformation(
                    "API gateway registered with broker as {AgentId} installation {InstallationId} for business {BusinessId}.",
                    _options.AgentId,
                    _options.InstallationId,
                    _options.BusinessId);

                await ProcessMessagesAsync(broker, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "API gateway broker connection failed. Retrying in {RetryDelaySeconds} seconds.",
                    RetryDelay.TotalSeconds);
            }
            finally
            {
                _connection.Detach(broker);
                await StopBrokerAsync(broker);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private RegisterAgent CreateRegistration()
    {
        var registration = new RegisterAgent
        {
            AgentId = _options.AgentId,
            AgentVersion = _options.Version,
            InstallationId = _options.InstallationId,
            BusinessId = _options.BusinessId
        };

        registration.RequestedPublications.Add(AgentChatEvents.UserMessageReceivedEvent);
        registration.RequestedPublications.AddRange(CommunicationEvents.All);
        registration.RequestedSubscriptions.Add(AgentChatEvents.AssistantResponseChunkEvent);
        registration.RequestedSubscriptions.Add(AgentChatEvents.AssistantResponseCreatedEvent);
        registration.RequestedPermissions.Add("installation.route");

        return registration;
    }

    private async Task ProcessMessagesAsync(
        IAgentBrokerClient broker,
        CancellationToken cancellationToken)
    {
        await foreach (var message in broker.ReadAllAsync(cancellationToken))
        {
            switch (message.PayloadCase)
            {
                case BrokerToAgentMessage.PayloadOneofCase.Event:
                    HandleEvent(message.Event);
                    break;

                case BrokerToAgentMessage.PayloadOneofCase.Error:
                    _logger.LogWarning(
                        "Broker rejected an API gateway message. Code: {Code}. Message: {Message}",
                        message.Error.Code,
                        message.Error.Message);
                    if (Guid.TryParse(message.CorrelationId, out var turnId))
                    {
                        _router.Publish(turnId, new ChatStreamChunk(
                            0,
                            message.Error.Message,
                            IsFinal: true,
                            Error: message.Error.Code,
                            Kind: "error"));
                    }
                    break;

                case BrokerToAgentMessage.PayloadOneofCase.Shutdown:
                    _logger.LogInformation(
                        "Broker requested API gateway shutdown: {Reason}",
                        message.Shutdown.Reason);
                    return;
            }
        }
    }

    private void HandleEvent(DeliveredEvent evt)
    {
        if (evt.EventType != AgentChatEvents.AssistantResponseChunkEvent)
        {
            _logger.LogInformation(
                "API gateway received broker event {EventType} from {SourceAgentId} with subject {Subject}; ignoring for chat stream routing.",
                evt.EventType,
                evt.SourceAgentId,
                evt.Subject);

            return;
        }

        _logger.LogInformation(
            "API gateway received assistant chunk event {EventType} from {SourceAgentId} with subject {Subject} and event id {EventId}.",
            evt.EventType,
            evt.SourceAgentId,
            evt.Subject,
            evt.EventId);

        var chunk = JsonSerializer.Deserialize<AssistantResponseChunk>(
            evt.Payload.ToByteArray(),
            SerializerOptions);

        if (chunk is null || !Guid.TryParse(chunk.ConversationId, out var conversationId))
        {
            _logger.LogWarning(
                "Ignoring assistant chunk event with invalid conversation id {ConversationId}.",
                chunk?.ConversationId);
            return;
        }

        _router.Publish(
            chunk.TurnId == Guid.Empty ? conversationId : chunk.TurnId,
            new ChatStreamChunk(chunk.Sequence, chunk.Delta, chunk.IsFinal, chunk.Error, chunk.Kind, chunk.Metadata, chunk.Attempt));

        _logger.LogInformation(
            "API gateway routed assistant chunk for conversation {ConversationId}. Sequence {Sequence}. IsFinal {IsFinal}. Error {Error}. DeltaLength {DeltaLength}.",
            conversationId,
            chunk.Sequence,
            chunk.IsFinal,
            chunk.Error,
            chunk.Delta.Length);
    }

    private async Task StopBrokerAsync(IAgentBrokerClient broker)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await broker.StopAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "API gateway broker client did not stop cleanly.");
        }
    }
}
