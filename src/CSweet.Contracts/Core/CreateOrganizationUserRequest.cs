using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateOrganizationUserRequest(
    [Required] string DisplayName,
    string? Email,
    int PermissionLevel);
