using System.Net.Http.Json;
using CSweet.Contracts.Planning;

namespace CSweet.UI.Services;

public sealed class OrganizationApiClient : IOrganizationApiClient
{
    private readonly HttpClient _httpClient;

    public OrganizationApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<OrganizationResponse>>("api/planning/organizations", cancellationToken)
            ?? [];
    }

    public async Task<OrganizationResponse> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<OrganizationResponse>($"api/planning/organizations/{id}", cancellationToken)
            ?? throw new ApiClientException(System.Net.HttpStatusCode.NotFound, "Organization not found.");
    }

    public async Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/planning/organizations", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<OrganizationResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Create response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to create organization.");
    }

    public async Task<OrganizationResponse> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/planning/organizations/{id}", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<OrganizationResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Update response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to update organization.");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/planning/organizations/{id}", cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.NoContent && !response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken);
            throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to delete organization.");
        }
    }
}
