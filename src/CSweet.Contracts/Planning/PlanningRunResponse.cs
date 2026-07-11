namespace CSweet.Contracts.Planning;

public sealed record PlanningRunResponse(
    Guid OrganizationId,
    string WorkflowKey,
    IReadOnlyList<PlanningTaskResponse> Tasks,
    DateTimeOffset StartedAt);
