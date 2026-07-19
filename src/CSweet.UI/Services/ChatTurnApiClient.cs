using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Contracts.Core;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CSweet.UI.Services;

public sealed class ChatTurnApiClient(HttpClient http) : IChatTurnApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ChatTurnResponse>> ListTurnsAsync(
        Guid organizationId, Guid chatId, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<ChatTurnResponse>>(BasePath(organizationId, chatId), cancellationToken) ?? [];

    public Task<ChatTurnResponse?> GetTurnAsync(
        Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default) =>
        http.GetFromJsonAsync<ChatTurnResponse>($"{BasePath(organizationId, chatId)}/{turnId}", cancellationToken);

    public async Task<IReadOnlyList<ChatTurnTraceEventResponse>> GetTurnTraceAsync(
        Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<IReadOnlyList<ChatTurnTraceEventResponse>>(
            $"{BasePath(organizationId, chatId)}/{turnId}/trace", cancellationToken) ?? [];

    public async IAsyncEnumerable<ChatTurnTraceEventResponse> StreamTurnEventsAsync(
        Guid organizationId, Guid chatId, Guid turnId, long afterSequence = -1,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{BasePath(organizationId, chatId)}/{turnId}/events?afterSequence={afterSequence}");
        request.SetBrowserResponseStreamingEnabled(true);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var traceEvent = JsonSerializer.Deserialize<ChatTurnTraceEventResponse>(line["data:".Length..].Trim(), JsonOptions);
            if (traceEvent is not null) yield return traceEvent;
        }
    }

    public async Task<ChatTurnStartResponse> RetryTurnAsync(
        Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"{BasePath(organizationId, chatId)}/{turnId}/retry", null, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ApiClientException(response.StatusCode, await ErrorAsync(response, cancellationToken));
        return await response.Content.ReadFromJsonAsync<ChatTurnStartResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty retry turn.");
    }

    public async Task<ChatTurnResponse> CancelTurnAsync(
        Guid organizationId, Guid chatId, Guid turnId, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsync($"{BasePath(organizationId, chatId)}/{turnId}/cancel", null, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ApiClientException(response.StatusCode, await ErrorAsync(response, cancellationToken));
        return await response.Content.ReadFromJsonAsync<ChatTurnResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty cancelled turn.");
    }

    private static string BasePath(Guid organizationId, Guid chatId) =>
        $"api/organizations/{organizationId}/communications/hub/chats/{chatId}/turns";

    private static async Task<string> ErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return error?.Message ?? error?.Error ?? "The chat turn request failed.";
        }
        catch (JsonException)
        {
            return "The chat turn request failed.";
        }
    }

    private sealed record ApiErrorResponse(string? Error, string? Message);
}
