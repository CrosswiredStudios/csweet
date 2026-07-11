using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record UpdateOrganizationRequest(
    string? Name,
    string? Industry,
    string? Mission,
    string? Stage,
    string? PrimaryGoal,
    string? ConstraintsJson);
