namespace CSweet.Infrastructure.Auth;

public sealed class RootRecoveryCode
{
    public Guid Id { get; set; }
    public Guid ApplicationUserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
}
