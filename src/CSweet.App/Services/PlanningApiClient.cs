using System.Net.Http.Json;
using CSweet.Contracts.Planning;

namespace CSweet.App.Services;

public sealed class PlanningApiClient : IPlanningApiClient
{
    private readonly HttpClient _httpClient;

    public PlanningApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PlanningRunResponse> StartRunAsync(StartPlanningRunRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/organizations/{request.OrganizationId}/planning-runs", request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PlanningRunResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Start run response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to start planning run.");
    }

    public async Task<PlanningStatusResponse> GetStatusAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<PlanningStatusResponse>(
                $"api/organizations/{organizationId}/planning-runs/{Uri.EscapeDataString(workflowKey)}", cancellationToken)
            ?? throw new ApiClientException(System.Net.HttpStatusCode.NotFound, "Planning run not found.");
    }

    public async Task<PlanningActionResponse> RunNextTaskAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/organizations/{organizationId}/planning-runs/{Uri.EscapeDataString(workflowKey)}/run-next",
            null, cancellationToken);

        return await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "Run next task response was empty.");
    }

    public async Task<PlanningActionResponse> CancelRunAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/organizations/{organizationId}/planning-runs/{Uri.EscapeDataString(workflowKey)}/cancel",
            null, cancellationToken);

        return await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "Cancel response was empty.");
    }

    public async Task<PlanningActionResponse> ResetRunAsync(Guid organizationId, string workflowKey, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(
            $"api/organizations/{organizationId}/planning-runs/{Uri.EscapeDataString(workflowKey)}/reset",
            null, cancellationToken);

        return await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "Reset response was empty.");
    }

    public async Task<IReadOnlyList<PlanningDocumentResponse>> ListDocumentsAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<PlanningDocumentResponse>>(
                $"api/organizations/{organizationId}/documents", cancellationToken)
            ?? [];
    }

    public async Task<PlanningDocumentResponse> GetDocumentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<PlanningDocumentResponse>($"api/documents/{id}", cancellationToken)
            ?? throw new ApiClientException(System.Net.HttpStatusCode.NotFound, "Document not found.");
    }

    public async Task<PlanningDocumentResponse> GenerateDocumentAsync(GeneratePlanningDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/organizations/{request.OrganizationId}/documents/generate", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<PlanningDocumentResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Generate response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<PlanningActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to generate document.");
    }

    public async Task<PlanningDocumentResponse> UpdateDocumentContentAsync(Guid id, string content, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsync(
            $"api/organizations/{id}/documents/{id}/content",
            new StringContent(content), cancellationToken);

        return await response.Content.ReadFromJsonAsync<PlanningDocumentResponse>(cancellationToken)
            ?? throw new ApiClientException(response.StatusCode, "Update response was empty.");
    }

    public async Task<IReadOnlyList<PlanningWorkflowResponse>> ListWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<PlanningWorkflowResponse>>("api/planning-workflows", cancellationToken)
            ?? [];
    }
}
