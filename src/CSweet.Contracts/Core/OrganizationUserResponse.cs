namespace CSweet.Contracts.Core;

public sealed record OrganizationUserResponse(
    Guid Id,
    Guid OrganizationId,
    string DisplayName,
    string? Email,
    int PermissionLevel,
    DateTimeOffset CreatedAt);
