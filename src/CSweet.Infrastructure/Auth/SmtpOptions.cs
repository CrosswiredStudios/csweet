namespace CSweet.Infrastructure.Auth;

public sealed class SmtpOptions
{
    public const string SectionName = "CSweet:Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "C-Sweet";
    public string PublicAppUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(FromAddress) &&
        Uri.TryCreate(PublicAppUrl, UriKind.Absolute, out _);
}
