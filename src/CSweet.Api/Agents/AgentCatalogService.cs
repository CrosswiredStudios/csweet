using System.Text.Json;
using CSweet.Agent.Contracts.Packaging;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Api.Agents;

public interface IAgentCatalogService
{
    Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed class AgentCatalogService : IAgentCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly CSweetDbContext _dbContext;
    private readonly ILogger<AgentCatalogService> _logger;

    public AgentCatalogService(
        CSweetDbContext dbContext,
        ILogger<AgentCatalogService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgentCatalogItemResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var packageVersions = await _dbContext.AgentPackageVersions
            .AsNoTracking()
            .Where(x => x.Status == AgentPackageVersionStatus.Approved || x.Status == AgentPackageVersionStatus.Built)
            .OrderByDescending(x => x.ImportedAt)
            .ToListAsync(cancellationToken);

        var manifests = packageVersions.Select(package =>
        {
            try
            {
                return JsonSerializer.Deserialize<AgentManifest>(package.ManifestJson, SerializerOptions);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Ignoring invalid manifest for imported package version {PackageVersionId}.", package.Id);
                return null;
            }
        });

        return manifests
            .Where(manifest => manifest is not null)
            .Cast<AgentManifest>()
            .GroupBy(manifest => manifest.Id, StringComparer.Ordinal)
            .Select(group => group.First())
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
}
