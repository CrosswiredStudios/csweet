using CSweet.Agent.Contracts.Grpc;

namespace CSweet.Agent.SDK;

public interface IAgentBrokerClient : IAsyncDisposable
{
    Task StartAsync(RegisterAgent registration, CancellationToken cancellationToken);

    IAsyncEnumerable<BrokerToAgentMessage> ReadAllAsync(CancellationToken cancellationToken);

    Task PublishEventAsync(
        PublishEvent message,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task<CapabilityResult> InvokeCapabilityAsync(
        RequestCapability request,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task SendCapabilityResultAsync(
        CapabilityResult result,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken);
}
