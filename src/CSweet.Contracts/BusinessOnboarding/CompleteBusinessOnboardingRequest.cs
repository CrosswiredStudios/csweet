using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.BusinessOnboarding;

public sealed record CompleteBusinessOnboardingRequest(
    [Required] string BusinessName,
    string? Industry,
    string? Stage,
    [Required] string PrimaryGoal,
    IReadOnlyList<string>? Constraints,
    string? PreferredOperatingStyle);
