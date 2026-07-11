using CSweet.Contracts.BusinessOnboarding;

namespace CSweet.Application.BusinessOnboarding;

public interface IBusinessOnboardingService
{
    Task<BusinessOnboardingActionResponse> CompleteAsync(
        CompleteBusinessOnboardingRequest request,
        CancellationToken cancellationToken = default);
}
