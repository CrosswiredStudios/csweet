namespace CSweet.Contracts.BusinessOnboarding;

public sealed record CompleteBusinessOnboardingResponse(
    Guid OrganizationId,
    int CreatedRoleCount,
    int CreatedTaskCount,
    Guid DefaultWorkerId,
    string NextRoute);
