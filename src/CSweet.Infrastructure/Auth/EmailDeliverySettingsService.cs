using System.Net.Mail;
using CSweet.Application.Setup;
using CSweet.Contracts.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Auth;

public sealed class EmailDeliverySettingsService : IEmailDeliverySettingsService
{
    private readonly CSweetDbContext _dbContext;
    private readonly IEmailDeliveryConfigurationProvider _provider;
    private readonly IAccountEmailSender _sender;

    public EmailDeliverySettingsService(
        CSweetDbContext dbContext,
        IEmailDeliveryConfigurationProvider provider,
        IAccountEmailSender sender)
    {
        _dbContext = dbContext;
        _provider = provider;
        _sender = sender;
    }

    public async Task<EmailDeliverySettingsResponse> GetAsync(CancellationToken cancellationToken = default) =>
        ToResponse(await _provider.GetAsync(cancellationToken));

    public async Task<EmailDeliveryActionResponse> UpdateAsync(
        UpdateEmailDeliverySettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Host) || request.Port is < 1 or > 65535 ||
            string.IsNullOrWhiteSpace(request.FromAddress) ||
            string.IsNullOrWhiteSpace(request.FromName) ||
            !Uri.TryCreate(request.PublicAppUrl, UriKind.Absolute, out _))
        {
            return Failure("validation_error", "Complete the required email delivery fields with valid values.");
        }

        try { _ = new MailAddress(request.FromAddress.Trim()); }
        catch (FormatException) { return Failure("validation_error", "Sender email address is invalid."); }

        var now = DateTimeOffset.UtcNow;
        var configuration = await _dbContext.EmailDeliveryConfigurations
            .OrderBy(x => x.ConfiguredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (configuration is null)
        {
            configuration = new EmailDeliveryConfiguration
            {
                Id = Guid.NewGuid(),
                ConfiguredAt = now
            };
            _dbContext.EmailDeliveryConfigurations.Add(configuration);
        }

        configuration.Host = request.Host.Trim();
        configuration.Port = request.Port;
        configuration.EnableSsl = request.EnableSsl;
        configuration.UserName = TrimOrNull(request.UserName);
        configuration.FromAddress = request.FromAddress.Trim();
        configuration.FromName = request.FromName.Trim();
        configuration.PublicAppUrl = request.PublicAppUrl.TrimEnd('/');
        configuration.LastTestSucceededAt = null;
        configuration.UpdatedAt = now;

        if (request.ClearPassword)
        {
            configuration.EncryptedPassword = null;
        }
        else if (request.Password is not null)
        {
            configuration.EncryptedPassword = string.IsNullOrEmpty(request.Password)
                ? null
                : _provider.Encrypt(request.Password);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new EmailDeliveryActionResponse(true, null, "Email delivery settings saved.", await GetAsync(cancellationToken));
    }

    public async Task<EmailDeliveryActionResponse> TestAsync(Guid applicationUserId, CancellationToken cancellationToken = default)
    {
        var email = await _dbContext.Users
            .Where(x => x.Id == applicationUserId)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(email))
        {
            return Failure("user_not_found", "The administrator email could not be found.");
        }

        try
        {
            await _sender.SendTestAsync(email, cancellationToken);
        }
        catch
        {
            var failedConfiguration = await _dbContext.EmailDeliveryConfigurations
                .OrderBy(x => x.ConfiguredAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (failedConfiguration is not null && failedConfiguration.LastTestSucceededAt.HasValue)
            {
                failedConfiguration.LastTestSucceededAt = null;
                failedConfiguration.UpdatedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return Failure("email_delivery_failed", "The test email could not be delivered. Check the SMTP settings and try again.");
        }

        var configuration = await _dbContext.EmailDeliveryConfigurations
            .OrderBy(x => x.ConfiguredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (configuration is null)
        {
            var effective = await _provider.GetAsync(cancellationToken);
            configuration = new EmailDeliveryConfiguration
            {
                Id = Guid.NewGuid(),
                Host = effective.Host,
                Port = effective.Port,
                EnableSsl = effective.EnableSsl,
                UserName = effective.UserName,
                EncryptedPassword = string.IsNullOrWhiteSpace(effective.Password) ? null : _provider.Encrypt(effective.Password),
                FromAddress = effective.FromAddress,
                FromName = effective.FromName,
                PublicAppUrl = effective.PublicAppUrl,
                ConfiguredAt = DateTimeOffset.UtcNow
            };
            _dbContext.EmailDeliveryConfigurations.Add(configuration);
        }

        if (configuration is not null)
        {
            configuration.LastTestSucceededAt = DateTimeOffset.UtcNow;
            configuration.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new EmailDeliveryActionResponse(true, null, $"Test email sent to {email}.", await GetAsync(cancellationToken));
    }

    private static EmailDeliverySettingsResponse ToResponse(EffectiveEmailDeliverySettings settings) => new(
        settings.Host,
        settings.Port,
        settings.EnableSsl,
        settings.UserName,
        !string.IsNullOrWhiteSpace(settings.Password),
        settings.FromAddress,
        settings.FromName,
        settings.PublicAppUrl,
        settings.IsConfigured,
        settings.IsConfigured && settings.LastTestSucceededAt.HasValue,
        settings.ConfiguredAt,
        settings.LastTestSucceededAt);

    private static EmailDeliveryActionResponse Failure(string code, string message) => new(false, code, message);
    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
