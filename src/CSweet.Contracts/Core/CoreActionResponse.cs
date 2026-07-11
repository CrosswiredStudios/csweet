namespace CSweet.Contracts.Core;

public sealed record CoreActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    OrganizationResponse? Organization = null,
    OrganizationUserResponse? OrganizationUser = null,
    RoleResponse? Role = null,
    StrategicObjectiveResponse? StrategicObjective = null,
    WorkerResponse? Worker = null,
    WorkTaskResponse? WorkTask = null,
    TaskRunResponse? TaskRun = null,
    ArtifactResponse? Artifact = null,
    ApprovalResponse? Approval = null);
