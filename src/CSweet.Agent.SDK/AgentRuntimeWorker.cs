using CSweet.Agent.Contracts.Packaging;
using CSweet.Agent.Contracts.Grpc;
using Google.Protobuf;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CSweet.Agent.SDK;

public sealed class AgentRuntimeWorker<TAgent> : BackgroundService
    where TAgent : class, ICSweetAgent
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly TAgent _agent;
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentBrokerOptions _options;
    private readonly ILogger<AgentRuntimeWorker<TAgent>> _logger;

    public AgentRuntimeWorker(
        TAgent agent,
        IServiceProvider serviceProvider,
        IOptions<AgentBrokerOptions> options,
        ILogger<AgentRuntimeWorker<TAgent>> logger)
    {
        _agent = agent;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var manifest = await AgentManifestLoader.LoadAsync(
            _options.ManifestPath,
            stoppingToken);

        if (!string.Equals(manifest.Id, _agent.AgentId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manifest agent id '{manifest.Id}' does not match implementation id '{_agent.AgentId}'.");
        }

        if (!string.Equals(manifest.Version, _agent.Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Manifest version '{manifest.Version}' does not match implementation version '{_agent.Version}'.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var broker = _serviceProvider.GetRequiredService<GrpcAgentBrokerClient>();

            try
            {
                await broker.StartAsync(CreateRegistration(manifest), stoppingToken);

                var context = new AgentRuntimeContext(
                    _options.BusinessId,
                    _options.InstallationId,
                    _options.RuntimeInstanceId,
                    _options.TickId,
                    broker);

                _logger.LogInformation(
                    "Agent {AgentId} version {AgentVersion} is connected for business {BusinessId}.",
                    _agent.AgentId,
                    _agent.Version,
                    _options.BusinessId);

                await ProcessMessagesAsync(broker, context, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Agent {AgentId} broker connection failed. Retrying in {RetryDelaySeconds} seconds.",
                    _agent.AgentId,
                    RetryDelay.TotalSeconds);
            }
            finally
            {
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

    private RegisterAgent CreateRegistration(AgentManifest manifest)
    {
        var registration = new RegisterAgent
        {
            AgentId = manifest.Id,
            AgentVersion = manifest.Version,
            InstallationId = _options.InstallationId,
            BusinessId = _options.BusinessId,
            RuntimeInstanceId = _options.RuntimeInstanceId,
            TickId = _options.TickId,
            WorkloadToken = _options.WorkloadToken
        };
        registration.DeclaredCapabilities.AddRange(manifest.Capabilities);
        registration.RequestedSubscriptions.AddRange(manifest.RequestedSubscriptions);
        registration.RequestedPublications.AddRange(manifest.RequestedPublications);

        return registration;
    }

    private async Task ProcessMessagesAsync(
        IAgentBrokerClient broker,
        AgentRuntimeContext context,
        CancellationToken stoppingToken)
    {
        await foreach (var message in broker.ReadAllAsync(stoppingToken))
        {
            switch (message.PayloadCase)
            {
                case BrokerToAgentMessage.PayloadOneofCase.Event:
                    await HandleEventAsync(message.Event, context, stoppingToken);
                    break;

                case BrokerToAgentMessage.PayloadOneofCase.CapabilityRequest:
                    await ExecuteCapabilityAsync(
                        broker,
                        message.CapabilityRequest,
                        message.CorrelationId,
                        context,
                        stoppingToken);
                    break;

                case BrokerToAgentMessage.PayloadOneofCase.Error:
                    _logger.LogWarning(
                        "Broker rejected an agent message. Code: {Code}. Message: {Message}",
                        message.Error.Code,
                        message.Error.Message);
                    break;

                case BrokerToAgentMessage.PayloadOneofCase.Shutdown:
                    _logger.LogInformation(
                        "Broker requested agent shutdown: {Reason}",
                        message.Shutdown.Reason);
                    return;
            }
        }
    }

    private async Task HandleEventAsync(
        DeliveredEvent message,
        AgentRuntimeContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await _agent.HandleEventAsync(message, context, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Agent {AgentId} failed while handling event {EventType}.",
                _agent.AgentId,
                message.EventType);
        }
    }

    private async Task ExecuteCapabilityAsync(
        IAgentBrokerClient broker,
        CapabilityRequest request,
        string correlationId,
        AgentRuntimeContext context,
        CancellationToken cancellationToken)
    {
        CapabilityResult result;

        try
        {
            var execution = await _agent.ExecuteCapabilityAsync(
                request,
                context,
                cancellationToken);

            result = new CapabilityResult
            {
                RequestId = request.RequestId,
                Succeeded = execution.Succeeded,
                ContentType = execution.ContentType,
                Payload = ByteString.CopyFrom(execution.Payload),
                Error = execution.Error ?? string.Empty
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Agent {AgentId} failed capability {Capability}.",
                _agent.AgentId,
                request.Capability);

            result = new CapabilityResult
            {
                RequestId = request.RequestId,
                Succeeded = false,
                ContentType = "application/json",
                Error = "The agent failed while processing the capability request."
            };
        }

        await broker.SendCapabilityResultAsync(
            result,
            correlationId,
            cancellationToken);
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
            _logger.LogDebug(exception, "Agent broker client did not stop cleanly.");
        }
    }
}
