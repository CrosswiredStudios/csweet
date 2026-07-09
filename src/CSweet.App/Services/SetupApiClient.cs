using System.Net.Http.Json;
using CSweet.Contracts.Setup;

namespace CSweet.App.Services;

public sealed class SetupApiClient : ISetupApiClient
{
    private readonly HttpClient _httpClient;

    public SetupApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<SetupStatusResponse>("api/setup/status", cancellationToken)
            ?? throw new ApiClientException(System.Net.HttpStatusCode.NoContent, "Setup status response was empty.");
    }

    public async Task<SetupActionResponse> CompleteStepAsync(string key, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/setup/steps/{Uri.EscapeDataString(key)}/complete", content: null, cancellationToken);
        return await ReadActionResponseAsync(response, cancellationToken);
    }

    public async Task<SetupActionResponse> CompleteSetupAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync("api/setup/complete", content: null, cancellationToken);
        return await ReadActionResponseAsync(response, cancellationToken);
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("api/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<SetupActionResponse> ReadActionResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var result = await response.Content.ReadFromJsonAsync<SetupActionResponse>(cancellationToken);
        if (result is not null)
        {
            return result;
        }

        throw new ApiClientException(response.StatusCode, $"Setup request failed with {(int)response.StatusCode}.");
    }
}
