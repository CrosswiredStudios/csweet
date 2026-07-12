using System.Text.Json;
using CSweet.Agent.Contracts.Packaging;

namespace CSweet.Agent.SDK;

public static class AgentManifestLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<AgentManifest> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var resolvedPath = Path.IsPathRooted(manifestPath)
            ? manifestPath
            : Path.Combine(AppContext.BaseDirectory, manifestPath);

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                $"Agent manifest was not found at '{resolvedPath}'.",
                resolvedPath);
        }

        await using var stream = File.OpenRead(resolvedPath);
        var manifest = await JsonSerializer.DeserializeAsync<AgentManifest>(
            stream,
            SerializerOptions,
            cancellationToken);

        if (manifest is null)
        {
            throw new InvalidOperationException("Agent manifest could not be deserialized.");
        }

        Validate(manifest);
        return manifest;
    }

    private static void Validate(AgentManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidOperationException("Agent manifest id is required.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Version))
        {
            throw new InvalidOperationException("Agent manifest version is required.");
        }

        if (manifest.Runtime.MaximumConcurrentJobs < 1)
        {
            throw new InvalidOperationException(
                "Agent manifest runtime.maximumConcurrentJobs must be at least one.");
        }
    }
}
