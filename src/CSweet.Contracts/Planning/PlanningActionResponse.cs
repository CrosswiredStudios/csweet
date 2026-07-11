namespace CSweet.Contracts.Planning;

public sealed record PlanningActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    OrganizationResponse? Organization = null,
    PlanningRunResponse? PlanningRun = null,
    PlanningDocumentResponse? Document = null);
