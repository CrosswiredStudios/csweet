using CSweet.Contracts.Planning;

namespace CSweet.Application.Planning;

public interface IPlanningWorkflowService
{
    Task<IReadOnlyList<PlanningWorkflowResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<PlanningWorkflowResponse?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
}
