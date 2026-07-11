using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record CreateOrganizationRequest(
    [Required] string Name,
    string? Industry,
    string? Mission,
    string? Stage,
    string? PrimaryGoal,
    string? ConstraintsJson);
