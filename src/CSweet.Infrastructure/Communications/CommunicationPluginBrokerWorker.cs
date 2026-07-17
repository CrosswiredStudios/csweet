using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.Application.Communications;
using CSweet.Communications.Abstractions;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSweet.Infrastructure.Communications;

public sealed class CommunicationPluginBrokerWorker(
    IServiceProvider services,
    CommunicationPluginBrokerConnection connection,
    ILogger<CommunicationPluginBrokerWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var broker = services.GetRequiredService<GrpcAgentBrokerClient>();
            try
            {
                await broker.StartAsync(Registration(), stoppingToken);
                connection.Attach(broker);
                await foreach (var message in broker.ReadAllAsync(stoppingToken))
                {
                    if (message.PayloadCase == BrokerToAgentMessage.PayloadOneofCase.CapabilityRequest)
                        await HandleCapabilityAsync(broker, message, stoppingToken);
                    else if (message.PayloadCase == BrokerToAgentMessage.PayloadOneofCase.Shutdown) break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning(exception, "Communication plugin broker disconnected; retrying."); }
            finally
            {
                connection.Detach(broker);
                try { await broker.StopAsync(CancellationToken.None); } catch { }
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task HandleCapabilityAsync(IAgentBrokerClient broker, BrokerToAgentMessage message,
        CancellationToken cancellationToken)
    {
        var request = message.CapabilityRequest;
        var result = new CapabilityResult { RequestId = request.RequestId, ContentType = "application/json" };
        try
        {
            if (request.Capability != CommunicationPluginCapabilities.IngestMessage)
                throw new InvalidOperationException($"Unsupported platform communication capability '{request.Capability}'.");
            var input = JsonSerializer.Deserialize<CommunicationPluginIngressRequest>(request.Payload.Span, JsonOptions)
                ?? throw new InvalidOperationException("Communication ingress payload was empty.");
            using var scope = services.CreateScope();
            var response = await scope.ServiceProvider.GetRequiredService<ICommunicationIngressHandler>()
                .IngestAsync(input.PluginInstallationId, input.OrganizationId, input.Envelope, cancellationToken);
            // The durable ingress receipt is the transport acknowledgement. A domain-level
            // rejection is returned in the payload but must not make the provider retry forever.
            result.Succeeded = true;
            result.Error = string.Empty;
            result.Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions));
        }
        catch (Exception exception)
        {
            result.Succeeded = false;
            result.Error = exception.Message;
        }
        await broker.SendCapabilityResultAsync(result, message.CorrelationId, cancellationToken);
    }

    private static RegisterAgent Registration()
    {
        var registration = new RegisterAgent
        {
            AgentId = "com.csweet.communication-gateway",
            AgentVersion = "1.0.0",
            InstallationId = "platform-communication-gateway",
            BusinessId = "default"
        };
        registration.DeclaredCapabilities.Add(CommunicationPluginCapabilities.IngestMessage);
        registration.RequestedPermissions.Add("installation.route");
        return registration;
    }
}
