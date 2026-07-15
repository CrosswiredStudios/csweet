using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CSweet.Infrastructure.Persistence;

namespace CSweet.Infrastructure.Auth;

public sealed record EffectiveEmailDeliverySettings(
    string Host,
    int Port,
    bool EnableSsl,
    string? UserName,
    string? Password,
    string FromAddress,
    string FromName,
    string PublicAppUrl,
    DateTimeOffset? ConfiguredAt,
    DateTimeOffset? LastTestSucceededAt)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        Port is > 0 and <= 65535 &&
        !string.IsNullOrWhiteSpace(FromAddress) &&
        Uri.TryCreate(PublicAppUrl, UriKind.Absolute, out _);
}

public interface IEmailDeliveryConfigurationProvider
{
    Task<EffectiveEmailDeliverySettings> GetAsync(CancellationToken cancellationToken);
    string Encrypt(string value);
}

public sealed class EmailDeliveryConfigurationProvider : IEmailDeliveryConfigurationProvider
{
    private readonly CSweetDbContext _dbContext;
    private readonly SmtpOptions _environmentOptions;
    private readonly IDataProtector _protector;

    public EmailDeliveryConfigurationProvider(
        CSweetDbContext dbContext,
        IOptions<SmtpOptions> options,
        IDataProtectionProvider dataProtectionProvider)
    {
        _dbContext = dbContext;
        _environmentOptions = options.Value;
        _protector = dataProtectionProvider.CreateProtector("CSweet.EmailDelivery.Password.v1");
    }

    public async Task<EffectiveEmailDeliverySettings> GetAsync(CancellationToken cancellationToken)
    {
        var persisted = await _dbContext.EmailDeliveryConfigurations
            .OrderBy(x => x.ConfiguredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (persisted is not null)
        {
            return new EffectiveEmailDeliverySettings(
                persisted.Host,
                persisted.Port,
                persisted.EnableSsl,
                persisted.UserName,
                Decrypt(persisted.EncryptedPassword),
                persisted.FromAddress,
                persisted.FromName,
                persisted.PublicAppUrl,
                persisted.ConfiguredAt,
                persisted.LastTestSucceededAt);
        }

        return new EffectiveEmailDeliverySettings(
            _environmentOptions.Host,
            _environmentOptions.Port,
            _environmentOptions.EnableSsl,
            _environmentOptions.UserName,
            _environmentOptions.Password,
            _environmentOptions.FromAddress,
            _environmentOptions.FromName,
            _environmentOptions.PublicAppUrl,
            null,
            null);
    }

    public string Encrypt(string value) => _protector.Protect(value);

    private string? Decrypt(string? value) => string.IsNullOrWhiteSpace(value) ? null : _protector.Unprotect(value);
}
