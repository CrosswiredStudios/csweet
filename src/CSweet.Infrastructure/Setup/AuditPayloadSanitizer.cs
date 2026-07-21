using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CSweet.Infrastructure.Setup;

public sealed record AuditPayloadEvidence(
    string Sha256,
    long Size,
    string? Preview,
    bool Truncated);

public static class AuditPayloadSanitizer
{
    public const int MaximumPreviewBytes = 64 * 1024;

    private static readonly string[] SecretTerms =
    [
        "token", "authorization", "password", "secret", "apikey", "cookie",
        "credential", "recoverycode", "privatekey"
    ];

    public static AuditPayloadEvidence Capture(ReadOnlyMemory<byte> payload, string? contentType)
    {
        var bytes = payload.Span;
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (bytes.Length == 0)
            return new AuditPayloadEvidence(hash, 0, null, false);

        if (!IsJson(contentType))
            return new AuditPayloadEvidence(hash, bytes.Length, null, false);

        try
        {
            var node = JsonNode.Parse(bytes);
            Redact(node);
            var redacted = JsonSerializer.SerializeToUtf8Bytes(node, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var truncated = redacted.Length > MaximumPreviewBytes;
            var previewBytes = truncated ? redacted.AsSpan(0, MaximumPreviewBytes) : redacted.AsSpan();
            return new AuditPayloadEvidence(hash, bytes.Length, Encoding.UTF8.GetString(previewBytes), truncated);
        }
        catch (JsonException)
        {
            return new AuditPayloadEvidence(hash, bytes.Length, null, false);
        }
    }

    public static string? RedactJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try
        {
            var node = JsonNode.Parse(json);
            Redact(node);
            return node?.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsJson(string? contentType) =>
        contentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true;

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj.ToList())
                {
                    if (IsSecret(property.Key)) obj[property.Key] = "[REDACTED]";
                    else Redact(property.Value);
                }
                break;
            case JsonArray array:
                foreach (var child in array) Redact(child);
                break;
        }
    }

    private static bool IsSecret(string key)
    {
        var normalized = new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return SecretTerms.Any(normalized.Contains);
    }
}
