namespace CSweet.Contracts.Core;

public sealed record RoleResponse(
    Guid Id,
    Guid OrganizationId,
    string Name,
    string Description,
    string ResponsibilitiesJson,
    int AuthorityLevel,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
