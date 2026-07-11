using CSweet.Contracts.Planning;

namespace CSweet.Application.Planning;

public interface IPlanningRunService
{
    Task<PlanningActionResponse> StartAsync(StartPlanningRunRequest request, CancellationToken cancellationToken = default);
    Task<PlanningStatusResponse?> GetStatusAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> RunNextTaskAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> CancelAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> ResetAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default);
}
