namespace CSweet.Domain.Core;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Industry { get; set; }
    public string? Mission { get; set; }
    public string? Stage { get; set; }
    public string? PrimaryGoal { get; set; }
    public string? ConstraintsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
