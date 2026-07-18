using System.Net.Http.Json;
using CSweet.Contracts.BusinessOnboarding;

namespace CSweet.UI.Services;

public sealed class BusinessOnboardingApiClient : IBusinessOnboardingApiClient
{
    private readonly HttpClient _httpClient;

    public BusinessOnboardingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CompleteBusinessOnboardingResponse> CompleteAsync(
        CompleteBusinessOnboardingRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/business-onboarding/complete", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CompleteBusinessOnboardingResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Business onboarding response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<BusinessOnboardingActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Business onboarding failed.");
    }

    public async Task<CompleteChiefSetupResponse> AssignChiefAsync(
        Guid organizationId,
        CompleteChiefSetupRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/business-onboarding/{organizationId}/chief", request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<CompleteChiefSetupResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Chief setup response was empty.");
        var error = await response.Content.ReadFromJsonAsync<ChiefSetupActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Chief setup failed.");
    }
}
