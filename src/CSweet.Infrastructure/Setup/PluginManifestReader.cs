using System.Text;
using System.Text.Json;
using CSweet.Application.Setup;
using CSweet.Contracts.Plugins;

namespace CSweet.Infrastructure.Setup;

public sealed class PluginManifestReader : IPluginManifestReader
{
    public PluginManifestEnvelope Read(ReadOnlyMemory<byte> manifestBytes, string manifestFileName)
    {
        if (!string.Equals(manifestFileName, "csweet-plugin.json", StringComparison.Ordinal))
        {
            throw new JsonException("Legacy csweet-agent.json manifests are not supported. Use csweet-plugin.json manifestVersion 1.0.");
        }

        var jsonBytes = StripUtf8Bom(manifestBytes);
        using var document = JsonDocument.Parse(jsonBytes);
        var root = document.RootElement;
        var manifestVersion = Required(root, "manifestVersion");
        if (!string.Equals(manifestVersion, "1.0", StringComparison.Ordinal))
        {
            throw new JsonException($"Unsupported plugin manifestVersion '{manifestVersion}'. Expected '1.0'.");
        }

        var kind = Required(root, "kind");
        if (kind is not ("agent" or "service"))
        {
            throw new JsonException("Plugin manifest kind must be 'agent' or 'service'.");
        }

        _ = JsonSerializer.Deserialize<PluginManifest>(jsonBytes.Span, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false
        }) ?? throw new JsonException("Plugin manifest is empty.");
        return new PluginManifestEnvelope(
            manifestFileName,
            kind,
            Required(root, "id"),
            Required(root, "name"),
            Required(root, "version"),
            Encoding.UTF8.GetString(jsonBytes.Span));
    }

    private static ReadOnlyMemory<byte> StripUtf8Bom(ReadOnlyMemory<byte> bytes)
        => bytes.Length >= 3 && bytes.Span[0] == 0xEF && bytes.Span[1] == 0xBB && bytes.Span[2] == 0xBF
            ? bytes[3..]
            : bytes;

    private static string Required(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new JsonException($"Plugin manifest property '{propertyName}' is required.");
}
