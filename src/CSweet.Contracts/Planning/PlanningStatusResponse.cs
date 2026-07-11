namespace CSweet.Contracts.Planning;

public sealed record PlanningStatusResponse(
    Guid OrganizationId,
    string WorkflowKey,
    int TotalTasks,
    int CompletedTasks,
    int FailedTasks,
    int PendingTasks,
    int RunningTasks,
    bool IsComplete,
    bool HasFailures,
    IReadOnlyList<PlanningTaskResponse> Tasks);
