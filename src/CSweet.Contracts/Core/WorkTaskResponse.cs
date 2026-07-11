namespace CSweet.Contracts.Core;

public sealed record WorkTaskResponse(
    Guid Id,
    Guid OrganizationId,
    Guid? StrategicObjectiveId,
    Guid? AssignedRoleId,
    Guid? AssignedWorkerId,
    string Title,
    string Description,
    int Status,
    int Priority,
    DateTimeOffset? DueDate,
    bool RequiresApproval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
