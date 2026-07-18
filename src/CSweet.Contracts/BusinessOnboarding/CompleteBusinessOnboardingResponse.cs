namespace CSweet.Contracts.BusinessOnboarding;

public sealed record CompleteBusinessOnboardingResponse(
    Guid OrganizationId,
    int CreatedRoleCount,
    int CreatedTaskCount,
    Guid DefaultWorkerId,
    string NextRoute)
{
    public bool OrganizationActivated { get; init; }
    public Guid? ChiefOrganizationUserId { get; init; }
    public IReadOnlyList<string> ChiefReadinessWarnings { get; init; } = [];
}
