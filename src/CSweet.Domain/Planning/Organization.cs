namespace CSweet.Domain.Planning;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Description { get; set; }
    public string? Stage { get; set; }
    public string? Location { get; set; }
    public string? TeamSize { get; set; }
    public string? AnnualRevenue { get; set; }
    public string? StrategicGoals { get; set; }
    public string? KeyChallenges { get; set; }
    public string? CompetitiveAdvantages { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
