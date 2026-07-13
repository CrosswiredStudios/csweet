using System.Text.Json;
using CSweet.Agent.Contracts.Packaging;
using CSweet.Contracts.Agents;
using Microsoft.Extensions.Options;

namespace CSweet.Api.Agents;

public interface IAgentCatalogService
{
    Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed class AgentCatalogService : IAgentCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AgentCatalogOptions _options;
    private readonly ILogger<AgentCatalogService> _logger;

    public AgentCatalogService(
        IOptions<AgentCatalogOptions> options,
        ILogger<AgentCatalogService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var manifests = new List<(string Path, AgentManifest Manifest)>();

        foreach (var path in FindManifestPaths())
        {
            try
            {
                await using var stream = File.OpenRead(path);
                var manifest = await JsonSerializer.DeserializeAsync<AgentManifest>(
                    stream,
                    SerializerOptions,
                    cancellationToken);

                if (manifest is not null)
                {
                    manifests.Add((path, manifest));
                }
            }
            catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
            {
                _logger.LogWarning(exception, "Ignoring unreadable agent manifest {ManifestPath}.", path);
            }
        }

        return manifests
            .GroupBy(item => item.Manifest.Id, StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.Path.Length).First().Manifest)
            .OrderBy(manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)
            .Select(manifest => new AgentCatalogItemResponse(
                manifest.Id,
                manifest.Name,
                manifest.Version,
                manifest.Publisher.Id,
                manifest.Publisher.Name,
                manifest.Runtime.Type,
                manifest.Capabilities,
                manifest.RequestedPermissions))
            .ToList();
    }

    private IEnumerable<string> FindManifestPaths()
    {
        var roots = _options.ManifestSearchPaths.Count > 0
            ? _options.ManifestSearchPaths
            : FindDefaultSearchRoots();

        foreach (var root in roots.Select(ResolvePath).Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var path in Directory.EnumerateFiles(root, "csweet-agent.json", SearchOption.AllDirectories))
            {
                yield return path;
            }
        }
    }

    private static IReadOnlyList<string> FindDefaultSearchRoots()
    {
        var roots = new List<string>
        {
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var sourceRoot = Path.Combine(directory.FullName, "src");
            if (Directory.Exists(sourceRoot))
            {
                roots.Add(sourceRoot);
                break;
            }

            directory = directory.Parent;
        }

        return roots;
    }

    private static string ResolvePath(string path) =>
        Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
}
