namespace CSweet.Contracts.Core;

public sealed record UpdateRoleRequest(
    string? Name,
    string? Description,
    string? ResponsibilitiesJson,
    int? AuthorityLevel);
