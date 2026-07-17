using System.Text;
using System.Text.Json;
using CSweet.Application.Setup;

namespace CSweet.Infrastructure.Setup;

public sealed class PluginManifestReader : IPluginManifestReader
{
    public PluginManifestEnvelope Read(ReadOnlyMemory<byte> manifestBytes, string manifestFileName)
    {
        using var document = JsonDocument.Parse(manifestBytes);
        var root = document.RootElement;
        var kind = root.TryGetProperty("kind", out var kindElement) && !string.IsNullOrWhiteSpace(kindElement.GetString())
            ? kindElement.GetString()!
            : "agent";
        return new PluginManifestEnvelope(
            manifestFileName,
            kind,
            Required(root, "id"),
            Required(root, "name"),
            Required(root, "version"),
            Encoding.UTF8.GetString(manifestBytes.Span));
    }

    private static string Required(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && !string.IsNullOrWhiteSpace(property.GetString())
            ? property.GetString()!
            : throw new JsonException($"Plugin manifest property '{propertyName}' is required.");
}
