using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Api.Chat;
using CSweet.Application.Communications;
using Google.Protobuf;

namespace CSweet.Api.Communications;

public sealed class BrokerCommunicationEventPublisher(ApiGatewayBrokerConnection broker) : ICommunicationEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task PublishAsync(CommunicationEventPublication publication, CancellationToken cancellationToken = default)
    {
        var message = new PublishEvent
        {
            EventType = publication.EventType,
            SchemaVersion = "1.0",
            Subject = $"agent-installation/{publication.TargetInstallationId:D}/{publication.Subject}",
            ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(publication.Envelope, JsonOptions))
        };
        return broker.PublishEventAsync(message, publication.Envelope.EventId.ToString("N"), cancellationToken);
    }
}

public sealed class CommunicationEventOutboxWorker(
    IServiceProvider services,
    ILogger<CommunicationEventOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<ICommunicationEventOutboxDispatcher>();
                var publisher = scope.ServiceProvider.GetRequiredService<ICommunicationEventPublisher>();
                var count = await dispatcher.DispatchBatchAsync(publisher, cancellationToken: stoppingToken);
                await Task.Delay(count > 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Communication event dispatch failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
