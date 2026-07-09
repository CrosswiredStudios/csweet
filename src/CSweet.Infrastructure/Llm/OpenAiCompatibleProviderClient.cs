using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;

namespace CSweet.Infrastructure.Llm;

public sealed class OpenAiCompatibleProviderClient
{
    private const int ChatCapabilityMaxTokens = 256;
    private const int StreamingCapabilityMaxTokens = 128;
    private const int ToolCapabilityMaxTokens = 128;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public OpenAiCompatibleProviderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ModelDescriptor>> ListModelsAsync(
        LlmProviderProfile profile,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(profile, apiKey, HttpMethod.Get, "models");
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var models = new List<ModelDescriptor>();
        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProperty))
            {
                continue;
            }

            var id = idProperty.GetString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string? ownedBy = null;
            if (item.TryGetProperty("owned_by", out var ownedByProperty))
            {
                ownedBy = ownedByProperty.GetString();
            }

            models.Add(new ModelDescriptor(id, ownedBy));
        }

        return models;
    }

    public async Task<string> CompleteChatAsync(
        LlmProviderProfile profile,
        string apiKey,
        string prompt,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = profile.DefaultChatModel,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = 0,
            max_tokens = ChatCapabilityMaxTokens,
            stream = false
        };

        using var request = CreateJsonRequest(profile, apiKey, "chat/completions", body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);
        return ExtractMessageContent(document.RootElement);
    }

    public async Task<bool> CompleteStreamingChatAsync(
        LlmProviderProfile profile,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = profile.DefaultChatModel,
            messages = new[] { new { role = "user", content = "Return the word READY and nothing else." } },
            temperature = 0,
            max_tokens = StreamingCapabilityMaxTokens,
            stream = true
        };

        using var request = CreateJsonRequest(profile, apiKey, "chat/completions", body);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return !string.IsNullOrWhiteSpace(content);
    }

    public async Task<bool> RequestToolCallAsync(
        LlmProviderProfile profile,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            model = profile.DefaultChatModel,
            messages = new[] { new { role = "user", content = "Use the provided tool to get the current time." } },
            max_tokens = ToolCapabilityMaxTokens,
            tools = new[]
            {
                new
                {
                    type = "function",
                    function = new
                    {
                        name = "get_current_time",
                        description = "Gets the current time.",
                        parameters = new
                        {
                            type = "object",
                            properties = new { },
                            required = Array.Empty<string>()
                        }
                    }
                }
            },
            tool_choice = "auto"
        };

        using var request = CreateJsonRequest(profile, apiKey, "chat/completions", body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(content);

        if (!TryGetFirstChoiceMessage(document.RootElement, out var message) ||
            !message.TryGetProperty("tool_calls", out var toolCalls) ||
            toolCalls.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return toolCalls.EnumerateArray().Any(toolCall =>
            toolCall.TryGetProperty("function", out var function) &&
            function.TryGetProperty("name", out var name) &&
            string.Equals(name.GetString(), "get_current_time", StringComparison.Ordinal));
    }

    private static HttpRequestMessage CreateJsonRequest(
        LlmProviderProfile profile,
        string apiKey,
        string relativePath,
        object body)
    {
        var request = CreateRequest(profile, apiKey, HttpMethod.Post, relativePath);
        request.Content = JsonContent.Create(body, options: SerializerOptions);
        return request;
    }

    private static HttpRequestMessage CreateRequest(
        LlmProviderProfile profile,
        string apiKey,
        HttpMethod method,
        string relativePath)
    {
        var baseUri = new Uri(profile.BaseUrl.TrimEnd('/') + "/");
        var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var code = (int)response.StatusCode;
        var reason = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Auth failure.",
            HttpStatusCode.NotFound => "Model or provider endpoint was not found.",
            HttpStatusCode.RequestTimeout => "Provider request timed out.",
            _ => $"Provider returned HTTP {code}."
        };

        await response.Content.LoadIntoBufferAsync(cancellationToken);
        throw new LlmProviderHttpException(code, reason);
    }

    private static string ExtractMessageContent(JsonElement root)
    {
        return TryGetFirstChoiceMessage(root, out var message) &&
            message.TryGetProperty("content", out var content)
            ? content.GetString() ?? string.Empty
            : string.Empty;
    }

    private static bool TryGetFirstChoiceMessage(JsonElement root, out JsonElement message)
    {
        message = default;

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return false;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out message))
        {
            return false;
        }

        return true;
    }
}

public sealed class LlmProviderHttpException : Exception
{
    public LlmProviderHttpException(int statusCode, string safeMessage)
        : base(safeMessage)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
