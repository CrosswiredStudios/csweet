using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
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
                    "API gateway registered with broker as {AgentId}.",
                    _options.AgentId);

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

        registration.RequestedPublications.Add(PersonalAssistantChatEvents.UserMessageReceivedEvent);
        registration.RequestedSubscriptions.Add(PersonalAssistantChatEvents.AssistantResponseChunkEvent);
        registration.RequestedSubscriptions.Add(PersonalAssistantChatEvents.AssistantResponseCreatedEvent);

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
        if (evt.EventType != PersonalAssistantChatEvents.AssistantResponseChunkEvent)
        {
            return;
        }

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
            conversationId,
            new ChatStreamChunk(chunk.Sequence, chunk.Delta, chunk.IsFinal));
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
