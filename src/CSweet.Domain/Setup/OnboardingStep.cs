namespace CSweet.Domain.Setup;

public sealed class OnboardingStep
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public bool IsComplete { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
