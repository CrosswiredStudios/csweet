using CSweet.Contracts.Setup;

namespace CSweet.Application.Setup;

public interface IEmailDeliverySettingsService
{
    Task<EmailDeliverySettingsResponse> GetAsync(CancellationToken cancellationToken = default);
    Task<EmailDeliveryActionResponse> UpdateAsync(UpdateEmailDeliverySettingsRequest request, CancellationToken cancellationToken = default);
    Task<EmailDeliveryActionResponse> TestAsync(Guid applicationUserId, CancellationToken cancellationToken = default);
}
