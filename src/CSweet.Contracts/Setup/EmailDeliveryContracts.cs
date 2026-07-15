using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Setup;

public sealed record EmailDeliverySettingsResponse(
    string Host,
    int Port,
    bool EnableSsl,
    string? UserName,
    bool HasPassword,
    string FromAddress,
    string FromName,
    string PublicAppUrl,
    bool IsConfigured,
    bool IsReady,
    DateTimeOffset? ConfiguredAt,
    DateTimeOffset? LastTestSucceededAt);

public sealed record UpdateEmailDeliverySettingsRequest(
    [property: Required] string Host,
    [property: Range(1, 65535)] int Port,
    bool EnableSsl,
    string? UserName,
    string? Password,
    bool ClearPassword,
    [property: Required, EmailAddress] string FromAddress,
    [property: Required] string FromName,
    [property: Required, Url] string PublicAppUrl);

public sealed record EmailDeliveryActionResponse(
    bool Succeeded,
    string? ErrorCode,
    string Message,
    EmailDeliverySettingsResponse? Settings = null);
