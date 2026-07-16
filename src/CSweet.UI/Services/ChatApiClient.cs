using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CSweet.Contracts.Core;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CSweet.UI.Services;

public sealed class ChatApiClient : IChatApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public ChatApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ConversationResponse>> GetConversationsAsync(
        Guid organizationId,
        Guid agentOrganizationUserId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ConversationResponse>>(
            $"api/core/organizations/{organizationId}/conversations?agentOrganizationUserId={agentOrganizationUserId}",
            cancellationToken) ?? [];
    }

    public async Task<ConversationResponse> StartConversationAsync(
        Guid organizationId,
        Guid agentOrganizationUserId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/core/organizations/{organizationId}/conversations",
            new StartConversationRequest(agentOrganizationUserId),
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ConversationResponse>(cancellationToken)
                ?? throw new ApiClientException(response.StatusCode, "Empty conversation response.");
        }

        var error = await response.Content.ReadFromJsonAsync<ConversationActionResponse>(cancellationToken);
        throw new ApiClientException(response.StatusCode, error?.Message ?? "Failed to start conversation.");
    }

    public async Task<IReadOnlyList<ConversationMessageResponse>> GetMessagesAsync(
        Guid organizationId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ConversationMessageResponse>>(
            $"api/core/organizations/{organizationId}/conversations/{conversationId}/messages",
            cancellationToken) ?? [];
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        Guid organizationId,
        Guid conversationId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"api/core/organizations/{organizationId}/conversations/{conversationId}/messages/stream")
        {
            Content = JsonContent.Create(new SendChatMessageRequest(message))
        };

        // Fetch buffers response bodies by default in some browser runtimes. Opting into
        // response streaming lets each SSE event reach the Razor component immediately.
        request.SetBrowserResponseStreamingEnabled(true);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await TryReadErrorAsync(response, cancellationToken);
            throw new ApiClientException(response.StatusCode, error ?? "Failed to send message.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                !line.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            var json = line["data:".Length..].Trim();
            var chunk = JsonSerializer.Deserialize<StreamChunk>(json, SerializerOptions);
            if (chunk is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(chunk.Error))
            {
                throw new InvalidOperationException(chunk.Delta);
            }

            if (chunk.IsFinal)
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(chunk.Delta))
            {
                yield return chunk.Delta;
            }
        }
    }

    public async Task<ChatTurnStartResponse> StartTurnAsync(Guid organizationId, Guid conversationId, string message, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/core/organizations/{organizationId}/conversations/{conversationId}/turns",
            new StartChatTurnRequest(message), cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ApiClientException(response.StatusCode, await TryReadErrorAsync(response, cancellationToken) ?? "The chat turn could not be started.");
        return await response.Content.ReadFromJsonAsync<ChatTurnStartResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty chat turn.");
    }

    public Task<ChatTurnResponse?> GetTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default) =>
        _httpClient.GetFromJsonAsync<ChatTurnResponse>($"api/core/organizations/{organizationId}/turns/{turnId}", cancellationToken);

    public async Task<IReadOnlyList<ChatTurnTraceEventResponse>> GetTurnTraceAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatTurnTraceEventResponse>>(
            $"api/core/organizations/{organizationId}/turns/{turnId}/trace", cancellationToken) ?? [];

    public async IAsyncEnumerable<ChatTurnTraceEventResponse> StreamTurnEventsAsync(
        Guid organizationId, Guid turnId, long afterSequence = -1,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/core/organizations/{organizationId}/turns/{turnId}/events?afterSequence={afterSequence}");
        request.SetBrowserResponseStreamingEnabled(true);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var traceEvent = JsonSerializer.Deserialize<ChatTurnTraceEventResponse>(line["data:".Length..].Trim(), SerializerOptions);
            if (traceEvent is not null) yield return traceEvent;
        }
    }

    public async Task<ChatTurnStartResponse> RetryTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/core/organizations/{organizationId}/turns/{turnId}/retry", null, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new ApiClientException(response.StatusCode, await TryReadErrorAsync(response, cancellationToken) ?? "The turn could not be retried.");
        return await response.Content.ReadFromJsonAsync<ChatTurnStartResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty retry turn.");
    }

    public async Task CancelTurnAsync(Guid organizationId, Guid turnId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync($"api/core/organizations/{organizationId}/turns/{turnId}/cancel", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string?> TryReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken);
            return error?.Message ?? error?.Error;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record StreamChunk(int Sequence, string Delta, bool IsFinal, string? Error);

    private sealed record ApiErrorResponse(string? Error, string? Message);
}
