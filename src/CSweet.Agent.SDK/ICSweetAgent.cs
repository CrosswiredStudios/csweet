using CSweet.Agent.Contracts.Grpc;

namespace CSweet.Agent.SDK;

public interface ICSweetAgent
{
    string AgentId { get; }

    string Version { get; }

    Task HandleEventAsync(
        DeliveredEvent message,
        AgentRuntimeContext context,
        CancellationToken cancellationToken);

    Task<AgentCapabilityExecutionResult> ExecuteCapabilityAsync(
        CapabilityRequest request,
        AgentRuntimeContext context,
        CancellationToken cancellationToken);
}
