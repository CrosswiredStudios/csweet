using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;

namespace CSweet.Api.Chat;

public sealed class ApiGatewayBrokerConnection : IAgentBrokerClient
{
    private readonly object _sync = new();
    private IAgentBrokerClient? _current;

    internal void Attach(IAgentBrokerClient client)
    {
        lock (_sync)
        {
            _current = client;
        }
    }

    internal void Detach(IAgentBrokerClient client)
    {
        lock (_sync)
        {
            if (ReferenceEquals(_current, client))
            {
                _current = null;
            }
        }
    }

    public Task StartAsync(RegisterAgent registration, CancellationToken cancellationToken) =>
        Current.StartAsync(registration, cancellationToken);

    public IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(CancellationToken cancellationToken) =>
        Current.ReadAllAsync(cancellationToken);

    public Task PublishEventAsync(
        PublishEvent message,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        Current.PublishEventAsync(message, correlationId, cancellationToken);

    public Task<CapabilityResult> InvokeCapabilityAsync(
        RequestCapability request,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        Current.InvokeCapabilityAsync(request, correlationId, cancellationToken);

    public Task SendCapabilityResultAsync(
        CapabilityResult result,
        string? correlationId = null,
        CancellationToken cancellationToken = default) =>
        Current.SendCapabilityResultAsync(result, correlationId, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Current.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private IAgentBrokerClient Current
    {
        get
        {
            lock (_sync)
            {
                return _current ?? throw new InvalidOperationException(
                    "The API gateway is not currently connected to the agent broker.");
            }
        }
    }
}
