using CSweet.Contracts.Agents;

namespace CSweet.Application.Setup;

public interface IAgentInteractiveRuntimeService
{
    Task<AgentRuntimeReadinessResponse> EnsureReadyAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);

    Task<AgentRuntimeReadinessResponse> GetStatusAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);
}