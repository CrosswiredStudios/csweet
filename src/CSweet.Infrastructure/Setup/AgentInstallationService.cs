using System.Text.Json;
using CSweet.Agent.Contracts.Packaging;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentInstallationService : IAgentInstallationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditWriter;
    private readonly IAgentContainerRunner _containers;
    private readonly ILogger<AgentInstallationService> _logger;

    public AgentInstallationService(
        CSweetDbContext dbContext,
        IAuditEventWriter auditWriter,
        IAgentContainerRunner containers,
        ILogger<AgentInstallationService> logger)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
        _containers = containers;
        _logger = logger;
    }

    public async Task<AgentInstallationResponse> InstallAsync(
        Guid importId,
        InstallAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var packageVersion = await _dbContext.AgentPackageVersions
            .SingleOrDefaultAsync(x => x.Id == importId, cancellationToken)
            ?? throw new AgentInstallationException("The import preview was not found.");
        var settings = await GetSettingsAsync(cancellationToken);
        ValidateBusinessId(request.BusinessId);
        var businessId = request.BusinessId.Trim();

        if (!settings.EnableImportedAgents)
        {
            throw new AgentInstallationException("Imported agents are disabled in global runtime settings.");
        }

            if (packageVersion.Status is not (
                AgentPackageVersionStatus.Previewed or
                AgentPackageVersionStatus.Approved or
                AgentPackageVersionStatus.Built))
            {
                throw new AgentInstallationException("The imported agent version is not available for installation.");
            }

        if (await _dbContext.AgentInstallations.AnyAsync(
                x => x.PackageVersionId == importId && x.BusinessId == businessId,
                cancellationToken))
        {
            throw new AgentInstallationException("This agent version is already installed for the business.");
        }

        var manifest = DeserializeManifest(packageVersion.ManifestJson);
        var activationMode = ParseActivationMode(request.ActivationMode);
        var overlapPolicy = ParseOverlapPolicy(request.OverlapPolicy);
        ValidateSchedule(request.TickFrequencySeconds, request.MaxRuntimeSeconds, activationMode, settings);
        ValidateResources(request.MemoryMb, request.CpuPercent, settings);
        ValidateGrant("capabilities", request.GrantedCapabilities, manifest.Capabilities);
        ValidateGrant("subscriptions", request.GrantedSubscriptions, manifest.RequestedSubscriptions);
        ValidateGrant("publications", request.GrantedPublications, manifest.RequestedPublications);
        ValidateGrant("permissions", request.GrantedPermissions, manifest.RequestedPermissions);
        ValidateGrant("network access", request.GrantedNetworkAccess, manifest.RequestedNetworkAccess);

        var now = DateTimeOffset.UtcNow;
        var installation = new AgentInstallation
        {
            Id = Guid.NewGuid(),
            PackageVersionId = packageVersion.Id,
            BusinessId = businessId,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        var grant = new AgentInstallationGrant
        {
            Id = Guid.NewGuid(),
            AgentInstallationId = installation.Id,
            CapabilitiesJson = SerializeGrant(request.GrantedCapabilities),
            SubscriptionsJson = SerializeGrant(request.GrantedSubscriptions),
            PublicationsJson = SerializeGrant(request.GrantedPublications),
            PermissionsJson = SerializeGrant(request.GrantedPermissions),
            NetworkAccessJson = SerializeGrant(request.GrantedNetworkAccess),
            MaxRuntimeSeconds = request.MaxRuntimeSeconds,
            MemoryMb = request.MemoryMb,
            CpuPercent = request.CpuPercent,
            ApprovedAt = now
        };
        var schedule = new AgentSchedule
        {
            Id = Guid.NewGuid(),
            AgentInstallationId = installation.Id,
            ActivationMode = activationMode,
            TickFrequencySeconds = request.TickFrequencySeconds,
            NextTickAt = ComputeNextTick(activationMode, request.TickFrequencySeconds, now),
            MaxRuntimeSeconds = request.MaxRuntimeSeconds,
            MaxRetriesPerTick = 0,
            OverlapPolicy = overlapPolicy,
            IsEnabled = true
        };

        var shouldQueueBuild = packageVersion.Status != AgentPackageVersionStatus.Built &&
            !await _dbContext.AgentBuildJobs.AnyAsync(
                x => x.PackageVersionId == packageVersion.Id,
                cancellationToken);
        if (packageVersion.Status != AgentPackageVersionStatus.Built)
        {
            packageVersion.Status = AgentPackageVersionStatus.Approved;
        }
        if (shouldQueueBuild)
        {
            _dbContext.AgentBuildJobs.Add(new AgentBuildJob
            {
                Id = Guid.NewGuid(),
                PackageVersionId = packageVersion.Id,
                Attempt = 1,
                QueuedAt = now
            });
        }
        installation.PackageVersion = packageVersion;
        installation.Grant = grant;
        installation.Schedule = schedule;
        _dbContext.AgentInstallations.Add(installation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditWriter.WriteAsync(
            "agent-installation.approved",
            nameof(AgentInstallation),
            installation.Id,
            $"Installed {packageVersion.AgentId} {packageVersion.Version} for business {businessId}.",
            null,
            cancellationToken);

        return ToResponse(installation);
    }

    public async Task<IReadOnlyList<AgentInstallationResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var installations = await InstallationQuery()
            .OrderBy(x => x.PackageVersion!.AgentName)
            .ThenBy(x => x.BusinessId)
            .ToListAsync(cancellationToken);
        return installations.Select(ToResponse).ToList();
    }

    public async Task<AgentInstallationResponse?> GetAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await InstallationQuery()
            .SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken);
        return installation is null ? null : ToResponse(installation);
    }

    public async Task<AgentInstallationResponse> UpdateScheduleAsync(
        Guid installationId,
        UpdateAgentScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        var activationMode = ParseActivationMode(request.ActivationMode);
        var overlapPolicy = ParseOverlapPolicy(request.OverlapPolicy);
        ValidateSchedule(request.TickFrequencySeconds, request.MaxRuntimeSeconds, activationMode, settings);

        if (request.MaxRuntimeSeconds > installation.Grant!.MaxRuntimeSeconds)
        {
            throw new AgentInstallationException("Schedule max runtime cannot exceed the approved installation grant.");
        }

        var now = DateTimeOffset.UtcNow;
        installation.Schedule!.ActivationMode = activationMode;
        installation.Schedule.TickFrequencySeconds = request.TickFrequencySeconds;
        installation.Schedule.OverlapPolicy = overlapPolicy;
        installation.Schedule.MaxRuntimeSeconds = request.MaxRuntimeSeconds;
        installation.Schedule.IsEnabled = request.IsEnabled;
        ResetAutomaticStartupFailures(installation.Schedule);
        installation.Schedule.NextTickAt = request.IsEnabled
            ? ComputeNextTick(activationMode, request.TickFrequencySeconds, now)
            : null;
        installation.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteScheduleAuditAsync(installation, "agent-installation.schedule.updated", cancellationToken);
        return ToResponse(installation);
    }

    public async Task<AgentInstallationResponse> UpdateAsync(
        Guid installationId,
        UpdateAgentInstallationRequest request,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        var currentPackage = installation.PackageVersion!;
        var nextPackage = await _dbContext.AgentPackageVersions
            .Include(x => x.BuildJobs)
            .SingleOrDefaultAsync(x => x.Id == request.PackageVersionId, cancellationToken)
            ?? throw new AgentInstallationException("The selected agent update is no longer available.");

        if (nextPackage.PackageSourceId != currentPackage.PackageSourceId ||
            !string.Equals(nextPackage.AgentId, currentPackage.AgentId, StringComparison.Ordinal))
        {
            throw new AgentInstallationException("The selected package is not an update for this agent.");
        }

        if (await _dbContext.AgentInstallations.AnyAsync(
                x => x.Id != installation.Id &&
                     x.PackageVersionId == nextPackage.Id &&
                     x.BusinessId == installation.BusinessId,
                cancellationToken))
        {
            throw new AgentInstallationException(
                $"Agent version {nextPackage.Version} is already installed for business {installation.BusinessId}. Refresh the Agents page before trying again.");
        }

        if (SemanticVersionComparer.Compare(nextPackage.Version, currentPackage.Version) <= 0)
        {
            throw new AgentInstallationException("The selected package version is not newer than the installed version.");
        }

        if (nextPackage.Status is not (
                AgentPackageVersionStatus.Previewed or
                AgentPackageVersionStatus.Approved or
                AgentPackageVersionStatus.Built or
                AgentPackageVersionStatus.Failed))
        {
            throw new AgentInstallationException("The selected agent update is not available for installation.");
        }

        var manifest = DeserializeManifest(nextPackage.ManifestJson);
        var grant = installation.Grant!;
        grant.CapabilitiesJson = RetainRequestedGrants(grant.CapabilitiesJson, manifest.Capabilities);
        grant.SubscriptionsJson = RetainRequestedGrants(grant.SubscriptionsJson, manifest.RequestedSubscriptions);
        grant.PublicationsJson = RetainRequestedGrants(grant.PublicationsJson, manifest.RequestedPublications);
        grant.PermissionsJson = RetainRequestedGrants(grant.PermissionsJson, manifest.RequestedPermissions);
        grant.NetworkAccessJson = RetainRequestedGrants(grant.NetworkAccessJson, manifest.RequestedNetworkAccess);

        await RemoveRuntimeContainersAsync(installation, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var runtime in installation.RuntimeInstances.Where(x => AgentRuntimeInstance.IsActive(x.Status)))
        {
            var terminalStatus = runtime.Status == AgentRuntimeStatus.CompletionReported
                ? AgentRuntimeStatus.Failed
                : AgentRuntimeStatus.Cancelled;
            runtime.TransitionTo(terminalStatus, now, "Agent installation updated to a newer package version.");
        }
        var latestBuild = nextPackage.BuildJobs.OrderByDescending(x => x.Attempt).FirstOrDefault();
        var shouldQueueBuild = nextPackage.Status != AgentPackageVersionStatus.Built &&
            latestBuild?.Status is not (
                AgentBuildStatus.Queued or
                AgentBuildStatus.Cloning or
                AgentBuildStatus.Building);
        if (nextPackage.Status != AgentPackageVersionStatus.Built)
        {
            nextPackage.Status = AgentPackageVersionStatus.Approved;
        }
        if (shouldQueueBuild)
        {
            _dbContext.AgentBuildJobs.Add(new AgentBuildJob
            {
                Id = Guid.NewGuid(),
                PackageVersionId = nextPackage.Id,
                Attempt = (latestBuild?.Attempt ?? 0) + 1,
                QueuedAt = now
            });
        }

        installation.PackageVersionId = nextPackage.Id;
        installation.PackageVersion = nextPackage;
        ResetAutomaticStartupFailures(installation.Schedule!);
        installation.UpdatedAt = now;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(
                exception,
                "Could not update agent installation {AgentInstallationId} to package {PackageVersionId}.",
                installation.Id,
                nextPackage.Id);
            throw new AgentInstallationException(
                "The agent update could not be saved. Refresh the Agents page and try again; the installed version was not changed.",
                exception);
        }

        await _auditWriter.WriteAsync(
            "agent-installation.updated",
            nameof(AgentInstallation),
            installation.Id,
            $"Updated {currentPackage.AgentId} for business {installation.BusinessId} from " +
            $"{currentPackage.Version} ({currentPackage.CommitSha}) to {nextPackage.Version} ({nextPackage.CommitSha}).",
            null,
            cancellationToken);

        return ToResponse(installation);
    }

    public async Task<AgentInstallationResponse> RunNowAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        if (!installation.IsEnabled || !installation.Schedule!.IsEnabled)
        {
            throw new AgentInstallationException("The agent installation and schedule must be enabled to run now.");
        }
        if (installation.Schedule.ActivationMode == ActivationMode.AlwaysOn)
        {
            throw new AgentInstallationException("Run Now is unavailable for always-on agents because they start automatically.");
        }

        var now = DateTimeOffset.UtcNow;
        installation.Schedule.RunRequestedAt = now;
        installation.Schedule.NextTickAt = now;
        installation.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await WriteScheduleAuditAsync(installation, "agent-installation.run-requested", cancellationToken);
        return ToResponse(installation);
    }

    public async Task<AgentInstallationResponse> DisableAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        installation.IsEnabled = false;
        installation.Schedule!.IsEnabled = false;
        installation.Schedule.NextTickAt = null;
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await WriteScheduleAuditAsync(installation, "agent-installation.disabled", cancellationToken);
        return ToResponse(installation);
    }

    public async Task<AgentInstallationResponse> EnableAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await GetInstallationAsync(installationId, cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        if (!settings.EnableImportedAgents)
            throw new AgentInstallationException("Imported agents are disabled in global runtime settings.");
        var now = DateTimeOffset.UtcNow;
        installation.IsEnabled = true;
        installation.Schedule!.IsEnabled = true;
        ResetAutomaticStartupFailures(installation.Schedule);
        installation.Schedule.NextTickAt = ComputeNextTick(
            installation.Schedule.ActivationMode,
            installation.Schedule.TickFrequencySeconds,
            now);
        installation.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await WriteScheduleAuditAsync(installation, "agent-installation.enabled", cancellationToken);
        return ToResponse(installation);
    }

    public async Task<RemoveAgentInstallationResponse> RemoveAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var installation = await InstallationQuery()
            .SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken)
            ?? throw new AgentInstallationException("The agent installation was not found.");
        var package = installation.PackageVersion!;
        var settings = await GetSettingsAsync(cancellationToken);
        var assignedEmployees = await _dbContext.CoreOrganizationUsers
            .AsNoTracking()
            .Where(x => x.AgentInstallationId == installation.Id)
            .OrderBy(x => x.DisplayName)
            .Select(x => x.DisplayName)
            .ToListAsync(cancellationToken);
        if (assignedEmployees.Count > 0)
        {
            var names = string.Join(", ", assignedEmployees.Take(3));
            var remainder = assignedEmployees.Count > 3
                ? $" and {assignedEmployees.Count - 3} more"
                : string.Empty;
            throw new AgentInstallationException(
                $"This agent is assigned to {assignedEmployees.Count} employee(s): {names}{remainder}. " +
                "Remove those employees from the Employees page before removing the agent installation.");
        }
        var removePackage = !await _dbContext.AgentInstallations.AnyAsync(
            x => x.PackageVersionId == package.Id && x.Id != installation.Id,
            cancellationToken);

        if (removePackage && package.BuildJobs.Any(
                x => x.Status is AgentBuildStatus.Cloning or AgentBuildStatus.Building))
        {
            throw new AgentInstallationException(
                "The agent is currently building. Wait for the build to finish before removing it.");
        }

        await using var transaction = _dbContext.Database.IsRelational()
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        installation.IsEnabled = false;
        if (installation.Schedule is not null)
        {
            installation.Schedule.IsEnabled = false;
            installation.Schedule.NextTickAt = null;
        }
        installation.UpdatedAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RemoveRuntimeContainersAsync(installation, cancellationToken);

        var sourceId = package.PackageSourceId;
        var removeSource = removePackage && !await _dbContext.AgentPackageVersions.AnyAsync(
            x => x.PackageSourceId == sourceId && x.Id != package.Id,
            cancellationToken);
        var cleanupPaths = removePackage ? CaptureCleanupPaths(package) : new AgentCleanupPaths([], [], []);

        if (removePackage)
        {
            foreach (var queuedJob in package.BuildJobs.Where(x => x.Status == AgentBuildStatus.Queued))
            {
                queuedJob.TransitionTo(AgentBuildStatus.Cancelled, DateTimeOffset.UtcNow);
            }
        }

        _dbContext.AgentInstallations.Remove(installation);
        if (removePackage)
        {
            _dbContext.AgentPackageVersions.Remove(package);
            if (removeSource)
            {
                var source = await _dbContext.AgentPackageSources
                    .SingleOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
                if (source is not null)
                {
                    _dbContext.AgentPackageSources.Remove(source);
                }
            }
        }
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(
                exception,
                "Could not remove agent installation {AgentInstallationId} because it is still referenced.",
                installation.Id);
            throw new AgentInstallationException(
                "The agent could not be removed because another record still references it. Refresh Employees and remove any assignments before trying again.",
                exception);
        }

        var cleanupWarnings = removePackage ? CleanupFiles(cleanupPaths, settings) : 0;
        await _auditWriter.WriteAsync(
            "agent-installation.removed",
            nameof(AgentInstallation),
            installation.Id,
            $"Removed {package.AgentId} {package.Version} from business {installation.BusinessId}. " +
            $"Package removed: {removePackage}; source removed: {removeSource}; cleanup warnings: {cleanupWarnings}.",
            null,
            cancellationToken);

        return new RemoveAgentInstallationResponse(
            installation.Id,
            removePackage,
            removeSource,
            cleanupWarnings);
    }

    public async Task<IReadOnlyList<AgentRuntimeRunResponse>> ListRunsAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.AgentInstallations.AnyAsync(x => x.Id == installationId, cancellationToken))
            throw new AgentInstallationException("The agent installation was not found.");
        var runs = await _dbContext.AgentRuntimeInstances.AsNoTracking()
            .Include(x => x.Events)
            .Where(x => x.AgentInstallationId == installationId)
            .OrderByDescending(x => x.QueuedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        var settings = await GetSettingsAsync(cancellationToken);
        var maximumLogBytes = Math.Min(settings.DefaultContainerLogLimitMb * 1024 * 1024, 64 * 1024);
        foreach (var run in runs.Where(run => !string.IsNullOrWhiteSpace(run.ContainerId)))
        {
            try
            {
                run.LogExcerpt = await _containers.GetLogsAsync(
                    run.ContainerId!,
                    maximumLogBytes,
                    cancellationToken);
            }
            catch (AgentContainerException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not read live container output for runtime {RuntimeInstanceId}.",
                    run.Id);
            }
        }
        return runs.Select(ToRunResponse).ToList();
    }

    public async Task<AgentBuildLogResponse?> GetBuildLogAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.AgentInstallations.AsNoTracking()
            .Where(x => x.Id == installationId)
            .SelectMany(x => x.PackageVersion!.BuildJobs)
            .OrderByDescending(x => x.Attempt)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null) return null;
        if (string.IsNullOrWhiteSpace(job.LogPath) || !File.Exists(job.LogPath))
            return new AgentBuildLogResponse(job.Id, job.Status.ToString(), string.Empty, false);
        var settings = await GetSettingsAsync(cancellationToken);
        var maximumBytes = checked(settings.MaximumBuildLogMb * 1024 * 1024);
        await using var stream = new FileStream(job.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, true);
        var length = (int)Math.Min(stream.Length, maximumBytes);
        var bytes = new byte[length];
        var read = await stream.ReadAtLeastAsync(bytes, length, throwOnEndOfStream: false, cancellationToken: cancellationToken);
        return new AgentBuildLogResponse(job.Id, job.Status.ToString(), System.Text.Encoding.UTF8.GetString(bytes, 0, read), stream.Length > maximumBytes);
    }

    private IQueryable<AgentInstallation> InstallationQuery() =>
        _dbContext.AgentInstallations
            .Include(x => x.PackageVersion)!.ThenInclude(x => x!.BuildJobs)
            .Include(x => x.Grant)
            .Include(x => x.Schedule)
            .Include(x => x.RuntimeInstances).ThenInclude(x => x.Events);

    private async Task<AgentInstallation> GetInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken) =>
        await InstallationQuery().SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken)
            ?? throw new AgentInstallationException("The agent installation was not found.");

    private async Task<AgentRuntimeGlobalSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await _dbContext.AgentRuntimeGlobalSettings.SingleOrDefaultAsync(cancellationToken)
            ?? throw new AgentInstallationException("Agent runtime settings have not been seeded.");

    private async Task RemoveRuntimeContainersAsync(
        AgentInstallation installation,
        CancellationToken cancellationToken)
    {
        foreach (var runtime in installation.RuntimeInstances)
        {
            var containerIdentifier = runtime.ContainerId ?? runtime.ContainerName;
            if (string.IsNullOrWhiteSpace(containerIdentifier))
            {
                continue;
            }

            try
            {
                if (await _containers.InspectAsync(containerIdentifier, cancellationToken) is not null)
                {
                    await _containers.RemoveAsync(containerIdentifier, force: true, cancellationToken);
                }
            }
            catch (AgentContainerException exception)
            {
                throw new AgentInstallationException(
                    $"The runtime container could not be removed. The installation was disabled and can be removed again: {exception.Message}");
            }
        }
    }

    private static AgentCleanupPaths CaptureCleanupPaths(AgentPackageVersion package) => new(
        package.BuildJobs.Select(x => x.SourceWorkspacePath).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList(),
        package.BuildJobs.Select(x => x.LogPath).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList(),
        package.BuildJobs.Select(x => x.PackagePath).Append(package.PackagePath).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToList());

    private int CleanupFiles(AgentCleanupPaths paths, AgentRuntimeGlobalSettings settings)
    {
        var sourceRoot = ResolveStorageRoot(settings.AgentSourceRootPath, "CSWEET_AGENT_SOURCE_ROOT", "sources");
        var packageRoot = ResolveStorageRoot(settings.AgentPackageCachePath, "CSWEET_AGENT_PACKAGE_CACHE", "packages");
        var warnings = 0;
        foreach (var path in paths.SourceWorkspaces)
        {
            warnings += TryDeletePath(path, sourceRoot, isDirectory: true);
        }
        foreach (var path in paths.LogFiles)
        {
            warnings += TryDeletePath(path, packageRoot, isDirectory: false);
        }
        foreach (var path in paths.PackageDirectories)
        {
            warnings += TryDeletePath(path, packageRoot, isDirectory: true);
        }
        return warnings;
    }

    private int TryDeletePath(string path, string approvedRoot, bool isDirectory)
    {
        try
        {
            if (!IsInsideRoot(path, approvedRoot))
            {
                _logger.LogWarning("Refused to remove agent artifact outside approved root {ApprovedRoot}: {ArtifactPath}", approvedRoot, path);
                return 1;
            }

            if (isDirectory)
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Could not remove agent artifact {ArtifactPath}", path);
            return 1;
        }
    }

    private static string ResolveStorageRoot(string configuredPath, string environmentVariable, string childName)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath)) return Path.GetFullPath(configuredPath);
        var environmentPath = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentPath)) return Path.GetFullPath(environmentPath);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var state = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, ".csweet")
            : Path.Combine(localAppData, "CSweet");
        return Path.GetFullPath(Path.Combine(state, "agents", childName));
    }

    private static bool IsInsideRoot(string path, string root)
    {
        var relative = Path.GetRelativePath(root, Path.GetFullPath(path));
        return relative != "." && relative != ".." &&
            !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathRooted(relative);
    }

    private sealed record AgentCleanupPaths(
        IReadOnlyList<string> SourceWorkspaces,
        IReadOnlyList<string> LogFiles,
        IReadOnlyList<string> PackageDirectories);

    private async Task WriteScheduleAuditAsync(
        AgentInstallation installation,
        string eventType,
        CancellationToken cancellationToken) =>
        await _auditWriter.WriteAsync(
            eventType,
            nameof(AgentInstallation),
            installation.Id,
            $"Updated {installation.PackageVersion!.AgentId} for business {installation.BusinessId}.",
            null,
            cancellationToken);

    private static AgentManifest DeserializeManifest(string manifestJson)
    {
        try
        {
            return JsonSerializer.Deserialize<AgentManifest>(manifestJson, SerializerOptions)
                ?? throw new AgentInstallationException("The stored agent manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new AgentInstallationException($"The stored agent manifest is invalid: {exception.Message}");
        }
    }

    private static void ValidateBusinessId(string businessId)
    {
        if (string.IsNullOrWhiteSpace(businessId) || businessId.Length > 200)
        {
            throw new AgentInstallationException("Business ID is required and cannot exceed 200 characters.");
        }
    }

    private static void ValidateSchedule(
        int tickFrequencySeconds,
        int maxRuntimeSeconds,
        ActivationMode activationMode,
        AgentRuntimeGlobalSettings settings)
    {
        if (tickFrequencySeconds < settings.MinimumTickFrequencySeconds)
        {
            throw new AgentInstallationException(
                $"Tick frequency must be at least {settings.MinimumTickFrequencySeconds} seconds.");
        }

        if (maxRuntimeSeconds <= 0 || maxRuntimeSeconds > settings.DefaultMaxRuntimeSeconds)
        {
            throw new AgentInstallationException(
                $"Max runtime must be between 1 and {settings.DefaultMaxRuntimeSeconds} seconds.");
        }

        if (activationMode == ActivationMode.AlwaysOn && !settings.AllowAlwaysOnCommunityAgents)
        {
            throw new AgentInstallationException("Always-on community agents are disabled by global policy.");
        }
    }

    private static void ValidateResources(
        int memoryMb,
        int cpuPercent,
        AgentRuntimeGlobalSettings settings)
    {
        if (memoryMb <= 0 || memoryMb > settings.MaximumContainerMemoryMb)
        {
            throw new AgentInstallationException(
                $"Memory must be between 1 and {settings.MaximumContainerMemoryMb} MB.");
        }

        if (cpuPercent <= 0 || cpuPercent > settings.MaximumContainerCpuPercent)
        {
            throw new AgentInstallationException(
                $"CPU must be between 1 and {settings.MaximumContainerCpuPercent} percent.");
        }
    }

    private static void ValidateGrant(
        string grantName,
        IReadOnlyList<string>? granted,
        IReadOnlyList<string> requested)
    {
        if (granted is null || granted.Any(string.IsNullOrWhiteSpace))
        {
            throw new AgentInstallationException($"Granted {grantName} must contain only non-empty values.");
        }

        var requestedSet = requested.ToHashSet(StringComparer.Ordinal);
        var broaderValue = granted.FirstOrDefault(value => !requestedSet.Contains(value));
        if (broaderValue is not null)
        {
            throw new AgentInstallationException(
                $"Granted {grantName} cannot include '{broaderValue}' because the manifest did not request it.");
        }
    }

    private static ActivationMode ParseActivationMode(string value) =>
        Enum.TryParse<ActivationMode>(value, ignoreCase: false, out var activationMode) &&
        Enum.IsDefined(activationMode)
            ? activationMode
            : throw new AgentInstallationException("Activation mode must be AlwaysOn, Periodic, or Manual.");

    private static OverlapPolicy ParseOverlapPolicy(string value) =>
        Enum.TryParse<OverlapPolicy>(value, ignoreCase: false, out var overlapPolicy) &&
        Enum.IsDefined(overlapPolicy)
            ? overlapPolicy
            : throw new AgentInstallationException("Overlap policy must be Skip, Queue, or CancelPrevious.");

    private static DateTimeOffset? ComputeNextTick(
        ActivationMode activationMode,
        int tickFrequencySeconds,
        DateTimeOffset now) => activationMode switch
        {
            ActivationMode.AlwaysOn => now,
            ActivationMode.Periodic => now.AddSeconds(tickFrequencySeconds),
            _ => null
        };

    private static string SerializeGrant(IReadOnlyList<string> values) =>
        JsonSerializer.Serialize(values.Distinct(StringComparer.Ordinal).ToList(), SerializerOptions);

    private static string RetainRequestedGrants(string grantedJson, IReadOnlyList<string> requested)
    {
        var requestedSet = requested.ToHashSet(StringComparer.Ordinal);
        return SerializeGrant(DeserializeGrant(grantedJson).Where(requestedSet.Contains).ToList());
    }

    private static IReadOnlyList<string> DeserializeGrant(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, SerializerOptions) ?? [];

    private static AgentInstallationResponse ToResponse(AgentInstallation installation)
    {
        var package = installation.PackageVersion!;
        var grant = installation.Grant!;
        var schedule = installation.Schedule!;
        var build = package.BuildJobs.OrderByDescending(x => x.Attempt).FirstOrDefault();
        var runtime = installation.RuntimeInstances.OrderByDescending(x => x.QueuedAt).FirstOrDefault();
        return new AgentInstallationResponse(
            installation.Id,
            installation.PackageVersionId,
            installation.BusinessId,
            package.AgentId,
            package.AgentName,
            package.Version,
            package.PublisherName,
            package.CommitSha,
            installation.IsEnabled,
            DeserializeGrant(grant.CapabilitiesJson),
            DeserializeGrant(grant.SubscriptionsJson),
            DeserializeGrant(grant.PublicationsJson),
            DeserializeGrant(grant.PermissionsJson),
            DeserializeGrant(grant.NetworkAccessJson),
            grant.MemoryMb,
            grant.CpuPercent,
            new AgentScheduleResponse(
                schedule.Id,
                schedule.ActivationMode.ToString(),
                schedule.TickFrequencySeconds,
                schedule.NextTickAt,
                schedule.LastTickAt,
                schedule.LastCompletedAt,
                schedule.RunRequestedAt,
                schedule.MaxRuntimeSeconds,
                schedule.MaxRetriesPerTick,
                schedule.ConsecutiveStartupFailures,
                schedule.AutomaticStartSuppressedAt,
                schedule.OverlapPolicy.ToString(),
                schedule.IsEnabled),
            installation.CreatedAt,
            installation.UpdatedAt,
            build is null ? null : new AgentBuildSummaryResponse(
                build.Id, build.Status.ToString(), build.Attempt, build.QueuedAt, build.StartedAt,
                build.CompletedAt, !string.IsNullOrWhiteSpace(build.LogPath), build.FailureMessage),
            runtime is null ? null : ToRunResponse(runtime));
    }

    private static void ResetAutomaticStartupFailures(AgentSchedule schedule)
    {
        schedule.ConsecutiveStartupFailures = 0;
        schedule.AutomaticStartSuppressedAt = null;
    }

    private static AgentRuntimeRunResponse ToRunResponse(AgentRuntimeInstance runtime) => new(
        runtime.Id,
        runtime.TickId,
        runtime.Status.ToString(),
        runtime.Reason,
        runtime.QueuedAt,
        runtime.StartedAt,
        runtime.BrokerRegisteredAt,
        runtime.CompletionReportedAt,
        runtime.CompletedAt,
        runtime.Events.OrderBy(x => x.OccurredAt)
            .Select(x => new AgentRuntimeEventResponse(x.Status.ToString(), x.Reason, x.OccurredAt))
            .ToList(),
        runtime.LogExcerpt);
}
