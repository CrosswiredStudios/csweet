namespace CSweet.Domain.Setup;

public sealed class AgentPackageSource
{
    public Guid Id { get; set; }
    public string RepositoryUrl { get; set; } = string.Empty;
    public string Host { get; set; } = "github.com";
    public string RepositoryOwner { get; set; } = string.Empty;
    public string RepositoryName { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}