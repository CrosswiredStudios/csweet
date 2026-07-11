namespace CSweet.Contracts.Planning;

public sealed record PlanningWorkflowResponse(
    Guid Id,
    string Key,
    string DisplayName,
    string Description,
    bool IsEnabled,
    int SortOrder);
