using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateRoleRequest(
    [Required] string Name,
    string Description,
    string ResponsibilitiesJson,
    int AuthorityLevel);
