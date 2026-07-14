using System.Text.Json;
using CSweet.Agent.Contracts.Packaging;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentInstallationService : IAgentInstallationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CSweetDbContext _dbContext;
    private readonly IAuditEventWriter _auditWriter;

    public AgentInstallationService(CSweetDbContext dbContext, IAuditEventWriter auditWriter)
    {
        _dbContext = dbContext;
        _auditWriter = auditWriter;
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
        installation.Schedule.NextTickAt = request.IsEnabled
            ? ComputeNextTick(activationMode, request.TickFrequencySeconds, now)
            : null;
        installation.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await WriteScheduleAuditAsync(installation, "agent-installation.schedule.updated", cancellationToken);
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

    private IQueryable<AgentInstallation> InstallationQuery() =>
        _dbContext.AgentInstallations
            .Include(x => x.PackageVersion)
            .Include(x => x.Grant)
            .Include(x => x.Schedule);

    private async Task<AgentInstallation> GetInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken) =>
        await InstallationQuery().SingleOrDefaultAsync(x => x.Id == installationId, cancellationToken)
            ?? throw new AgentInstallationException("The agent installation was not found.");

    private async Task<AgentRuntimeGlobalSettings> GetSettingsAsync(CancellationToken cancellationToken) =>
        await _dbContext.AgentRuntimeGlobalSettings.SingleOrDefaultAsync(cancellationToken)
            ?? throw new AgentInstallationException("Agent runtime settings have not been seeded.");

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

    private static IReadOnlyList<string> DeserializeGrant(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, SerializerOptions) ?? [];

    private static AgentInstallationResponse ToResponse(AgentInstallation installation)
    {
        var package = installation.PackageVersion!;
        var grant = installation.Grant!;
        var schedule = installation.Schedule!;
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
                schedule.OverlapPolicy.ToString(),
                schedule.IsEnabled),
            installation.CreatedAt,
            installation.UpdatedAt);
    }
}
