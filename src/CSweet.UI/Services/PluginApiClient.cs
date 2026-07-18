using System.Net.Http.Json;
using System.Text.Json;
using CSweet.Contracts.Agents;

namespace CSweet.UI.Services;

public sealed class PluginApiClient(HttpClient httpClient) : IPluginApiClient
{
    public Task<AgentImportPreviewResponse> PreviewAsync(PreviewAgentImportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentImportPreviewResponse>(HttpMethod.Post, "api/plugins/imports/preview", request, cancellationToken);

    public Task<AgentInstallationResponse> InstallAsync(Guid importId, InstallAgentRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AgentInstallationResponse>(HttpMethod.Post, $"api/plugins/imports/{importId}/install", request, cancellationToken);

    public async Task<IReadOnlyList<AgentInstallationResponse>> ListAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<AgentInstallationResponse>>("api/plugins/installations", cancellationToken) ?? [];

    public Task SaveConfigurationAsync(Guid installationId, IReadOnlyDictionary<string, string> settings,
        CancellationToken cancellationToken = default)
    {
        var values = settings.ToDictionary(
            x => x.Key,
            x => JsonSerializer.SerializeToElement(x.Value),
            StringComparer.Ordinal);
        return SendNoContentAsync(HttpMethod.Put, $"api/plugins/installations/{installationId}/configuration",
            new UpdateAgentConfigurationRequest(values) { SchemaVersion = "1" }, cancellationToken);
    }

    public Task SetSecretAsync(Guid installationId, string key, string value,
        CancellationToken cancellationToken = default) =>
        SendNoContentAsync(HttpMethod.Put,
            $"api/plugins/installations/{installationId}/secrets/{Uri.EscapeDataString(key)}",
            new SetPluginSecretRequest(value), cancellationToken);

    public Task<AgentInstallationResponse> SetEnabledAsync(Guid installationId, bool enabled, CancellationToken cancellationToken = default) =>
        SendAsync<AgentInstallationResponse>(HttpMethod.Post,
            $"api/plugins/installations/{installationId}/{(enabled ? "enable" : "disable")}", null, cancellationToken);

    public Task<RemoveAgentInstallationResponse> RemoveAsync(Guid installationId, CancellationToken cancellationToken = default) =>
        SendAsync<RemoveAgentInstallationResponse>(HttpMethod.Delete, $"api/plugins/installations/{installationId}", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string uri, object? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri)
        {
            Content = content is null ? null : JsonContent.Create(content)
        };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Plugin API response was empty.");
        var error = await response.Content.ReadFromJsonAsync<PluginApiErrorResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Error ?? "Plugin operation failed.");
    }

    private async Task SendNoContentAsync(HttpMethod method, string uri, object content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, uri) { Content = JsonContent.Create(content) };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode) return;
        var error = await response.Content.ReadFromJsonAsync<PluginApiErrorResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Error ?? "Plugin operation failed.");
    }

    private sealed record PluginApiErrorResponse(string? Error);
}
