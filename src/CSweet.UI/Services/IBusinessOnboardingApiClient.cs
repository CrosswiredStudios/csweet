using CSweet.Contracts.BusinessOnboarding;

namespace CSweet.UI.Services;

public interface IBusinessOnboardingApiClient
{
    Task<CompleteBusinessOnboardingResponse> CompleteAsync(
        CompleteBusinessOnboardingRequest request,
        CancellationToken cancellationToken = default);
}
