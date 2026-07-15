namespace CSweet.Infrastructure.Auth;

public sealed class EmailDeliveryConfiguration
{
    public Guid Id { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string? UserName { get; set; }
    public string? EncryptedPassword { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "C-Sweet";
    public string PublicAppUrl { get; set; } = string.Empty;
    public DateTimeOffset ConfiguredAt { get; set; }
    public DateTimeOffset? LastTestSucceededAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
