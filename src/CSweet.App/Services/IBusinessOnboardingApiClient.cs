using CSweet.Contracts.BusinessOnboarding;

namespace CSweet.App.Services;

public interface IBusinessOnboardingApiClient
{
    Task<CompleteBusinessOnboardingResponse> CompleteAsync(
        CompleteBusinessOnboardingRequest request,
        CancellationToken cancellationToken = default);
}
