using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CSweet.Agent.Contracts.Packaging;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed partial class AgentImportPreviewService : IAgentImportPreviewService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CSweetDbContext _dbContext;
    private readonly IGitHubAgentRepositoryClient _repositoryClient;
    private readonly IAuditEventWriter _auditWriter;

    public AgentImportPreviewService(
        CSweetDbContext dbContext,
        IGitHubAgentRepositoryClient repositoryClient,
        IAuditEventWriter auditWriter)
    {
        _dbContext = dbContext;
        _repositoryClient = repositoryClient;
        _auditWriter = auditWriter;
    }

    public async Task<AgentImportPreviewResponse> PreviewAsync(
        PreviewAgentImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var repository = GitHubRepositoryUrlNormalizer.Normalize(request.RepositoryUrl);
        if (request.Ref?.Length > 255)
        {
            throw new AgentImportPreviewException("Git reference cannot exceed 255 characters.");
        }

        var defaultBranch = await _repositoryClient.GetDefaultBranchAsync(
            repository.Owner,
            repository.Name,
            cancellationToken);
        var reference = string.IsNullOrWhiteSpace(request.Ref) ? defaultBranch : request.Ref.Trim();
        var commitSha = await _repositoryClient.ResolveCommitShaAsync(
            repository.Owner,
            repository.Name,
            reference,
            cancellationToken);
        var manifestBytes = await _repositoryClient.GetRootManifestAsync(
            repository.Owner,
            repository.Name,
            commitSha,
            cancellationToken);

        AgentManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<AgentManifest>(manifestBytes, SerializerOptions)
                ?? throw new AgentImportPreviewException("Agent manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new AgentImportPreviewException(
                $"Agent manifest is not valid JSON: {exception.Message}",
                exception);
        }

        ValidateManifest(manifest);
        var warnings = CreateWarnings(manifest);
        var digest = Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow;

        var source = await _dbContext.AgentPackageSources
            .SingleOrDefaultAsync(x => x.RepositoryUrl == repository.RepositoryUrl, cancellationToken);
        if (source is null)
        {
            source = new AgentPackageSource
            {
                Id = Guid.NewGuid(),
                RepositoryUrl = repository.RepositoryUrl,
                Host = "github.com",
                RepositoryOwner = repository.Owner,
                RepositoryName = repository.Name,
                DefaultBranch = defaultBranch,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.AgentPackageSources.Add(source);
        }
        else
        {
            source.DefaultBranch = defaultBranch;
            source.UpdatedAt = now;
        }

        var version = await _dbContext.AgentPackageVersions
            .SingleOrDefaultAsync(
                x => x.PackageSourceId == source.Id &&
                     x.CommitSha == commitSha &&
                     x.ManifestDigest == digest,
                cancellationToken);
        var isNewVersion = version is null;
        if (version is null)
        {
            version = new AgentPackageVersion
            {
                Id = Guid.NewGuid(),
                PackageSourceId = source.Id,
                CommitSha = commitSha,
                ManifestDigest = digest,
                ManifestJson = Encoding.UTF8.GetString(manifestBytes),
                AgentId = manifest.Id,
                AgentName = manifest.Name,
                Version = manifest.Version,
                PublisherId = manifest.Publisher.Id,
                PublisherName = manifest.Publisher.Name,
                RuntimeType = manifest.Runtime.Type,
                ProjectPath = manifest.Runtime.ProjectPath,
                TargetFramework = manifest.Runtime.TargetFramework,
                DefaultActivationMode = manifest.Runtime.DefaultActivationMode,
                WarningsJson = JsonSerializer.Serialize(warnings, SerializerOptions),
                Status = AgentPackageVersionStatus.Previewed,
                ImportedAt = now
            };
            _dbContext.AgentPackageVersions.Add(version);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isNewVersion)
        {
            await _auditWriter.WriteAsync(
                "agent-import.previewed",
                nameof(AgentPackageVersion),
                version.Id,
                $"Previewed {manifest.Id} {manifest.Version} from {repository.RepositoryUrl}.",
                null,
                cancellationToken);
        }

        return ToResponse(version, source, manifest, warnings);
    }

    private static void ValidateManifest(AgentManifest manifest)
    {
        var errors = new List<string>();
        AddRequiredIdentifierError(manifest.Id, "id", errors);
        AddRequiredError(manifest.Name, "name", errors);

        if (string.IsNullOrWhiteSpace(manifest.Version) || !SemanticVersionRegex().IsMatch(manifest.Version))
        {
            errors.Add("Agent manifest version must be a semantic version such as 1.2.3.");
        }

        if (manifest.Publisher is null)
        {
            errors.Add("Agent manifest publisher is required.");
        }
        else
        {
            AddRequiredIdentifierError(manifest.Publisher.Id, "publisher.id", errors);
            AddRequiredError(manifest.Publisher.Name, "publisher.name", errors);
        }

        if (manifest.Runtime is null)
        {
            errors.Add("Agent manifest runtime is required.");
        }
        else
        {
            if (!string.Equals(manifest.Runtime.Type, "dotnet-project", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Imported GitHub agents must use runtime.type 'dotnet-project'.");
            }

            ValidateProjectPath(manifest.Runtime.ProjectPath, errors);

            if (string.IsNullOrWhiteSpace(manifest.Runtime.TargetFramework) ||
                !TargetFrameworkRegex().IsMatch(manifest.Runtime.TargetFramework))
            {
                errors.Add("runtime.targetFramework must be a .NET target framework such as net10.0.");
            }

            if (manifest.Runtime.DefaultActivationMode is not null &&
                manifest.Runtime.DefaultActivationMode is not ("AlwaysOn" or "Periodic" or "Manual"))
            {
                errors.Add("runtime.defaultActivationMode must be AlwaysOn, Periodic, or Manual.");
            }

            if (manifest.Runtime.MaximumConcurrentJobs < 1)
            {
                errors.Add("runtime.maximumConcurrentJobs must be at least one.");
            }
        }

        if (manifest.Protocol is null ||
            string.IsNullOrWhiteSpace(manifest.Protocol.MinimumVersion) ||
            string.IsNullOrWhiteSpace(manifest.Protocol.MaximumVersion))
        {
            errors.Add("Agent manifest protocol minimumVersion and maximumVersion are required.");
        }

        AddListError(manifest.Capabilities, "capabilities", errors);
        AddListError(manifest.RequestedSubscriptions, "requestedSubscriptions", errors);
        AddListError(manifest.RequestedPublications, "requestedPublications", errors);
        AddListError(manifest.RequestedPermissions, "requestedPermissions", errors);
        AddListError(manifest.RequestedNetworkAccess, "requestedNetworkAccess", errors);

        if (errors.Count > 0)
        {
            throw new AgentImportPreviewException(string.Join(" ", errors));
        }
    }

    private static void ValidateProjectPath(string? projectPath, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            errors.Add("runtime.projectPath is required for dotnet-project agents.");
            return;
        }

        var segments = projectPath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        if (Path.IsPathRooted(projectPath) ||
            projectPath.StartsWith('/') ||
            projectPath.StartsWith('\\') ||
            segments.Contains("..", StringComparer.Ordinal) ||
            !projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("runtime.projectPath must be a relative .csproj path without parent traversal.");
        }
    }

    private static IReadOnlyList<AgentManifestWarningResponse> CreateWarnings(AgentManifest manifest)
    {
        var warnings = new List<AgentManifestWarningResponse>();
        if (manifest.RequestedNetworkAccess.Count > 0)
        {
            warnings.Add(new AgentManifestWarningResponse(
                "network_access_requested",
                "This agent requests network access. Access remains denied until explicitly approved."));
        }

        if (manifest.RequestedPermissions.Count > 0)
        {
            warnings.Add(new AgentManifestWarningResponse(
                "permissions_requested",
                "Requested permissions are declarations only and must be approved during installation."));
        }

        if (manifest.Runtime.DefaultActivationMode == "AlwaysOn")
        {
            warnings.Add(new AgentManifestWarningResponse(
                "always_on_requested",
                "Always-on activation for community agents is subject to global policy."));
        }

        return warnings;
    }

    private static AgentImportPreviewResponse ToResponse(
        AgentPackageVersion version,
        AgentPackageSource source,
        AgentManifest manifest,
        IReadOnlyList<AgentManifestWarningResponse> warnings) =>
        new(
            version.Id,
            source.RepositoryUrl,
            version.CommitSha,
            version.ManifestDigest,
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Publisher.Id,
            manifest.Publisher.Name,
            manifest.Runtime.Type,
            manifest.Runtime.ProjectPath,
            manifest.Runtime.TargetFramework,
            manifest.Runtime.DefaultActivationMode,
            manifest.Capabilities,
            manifest.RequestedSubscriptions,
            manifest.RequestedPublications,
            manifest.RequestedPermissions,
            manifest.RequestedNetworkAccess,
            warnings,
            version.Status.ToString());

    private static void AddRequiredIdentifierError(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value) || !IdentifierRegex().IsMatch(value))
        {
            errors.Add($"Agent manifest {fieldName} must contain letters, numbers, dots, underscores, or hyphens.");
        }
    }

    private static void AddRequiredError(string? value, string fieldName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Agent manifest {fieldName} is required.");
        }
    }

    private static void AddListError(IReadOnlyList<string>? values, string fieldName, List<string> errors)
    {
        if (values is null || values.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add($"Agent manifest {fieldName} must be an array of non-empty strings.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9._-]{0,198}[A-Za-z0-9])?$")]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex("^\\d+\\.\\d+\\.\\d+(?:-[0-9A-Za-z.-]+)?(?:\\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemanticVersionRegex();

    [GeneratedRegex("^net\\d+\\.\\d+(?:-[A-Za-z0-9.-]+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex TargetFrameworkRegex();
}