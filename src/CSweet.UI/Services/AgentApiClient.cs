using System.Net.Http.Json;
using CSweet.Contracts.Agents;

namespace CSweet.UI.Services;

public sealed class AgentApiClient : IAgentApiClient
{
    private readonly HttpClient _httpClient;

    public AgentApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<AgentCatalogItemResponse>>(
                "api/agents",
                cancellationToken)
            ?? [];
    }

    public async Task<AgentImportPreviewResponse> PreviewImportAsync(
        PreviewAgentImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/agents/imports/preview",
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AgentImportPreviewResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Agent import preview response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<AgentApiErrorResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Error ?? "Agent import could not be previewed.");
    }

    public async Task<AgentConfigurationSchemaResponse> GetConfigurationAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"api/agents/{Uri.EscapeDataString(agentId)}/configuration",
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AgentConfigurationSchemaResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Agent configuration response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<AgentApiErrorResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Error ?? "Agent configuration could not be loaded.");
    }

    public async Task<AgentConfigurationUpdateResponse> UpdateConfigurationAsync(
        string agentId,
        UpdateAgentConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/agents/{Uri.EscapeDataString(agentId)}/configuration",
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<AgentConfigurationUpdateResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Agent configuration update response was empty.");
        }

        var error = await response.Content.ReadFromJsonAsync<AgentApiErrorResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Error ?? "Agent configuration could not be saved.");
    }

    private sealed record AgentApiErrorResponse(string? Error);
}
