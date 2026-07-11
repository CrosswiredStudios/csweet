namespace CSweet.Contracts.Planning;

public sealed record StartPlanningRunRequest(
    Guid OrganizationId,
    string WorkflowKey,
    Guid ProviderProfileId);
