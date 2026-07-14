using System.Security.Cryptography;
using System.Text;
using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentRuntimeManager(
    CSweetDbContext dbContext,
    IAgentContainerRunner containers,
    IOptions<AgentRuntimeManagerOptions> options,
    ILogger<AgentRuntimeManager> logger) : IAgentRuntimeManager
{
    private static readonly AgentRuntimeStatus[] ContainerActiveStatuses =
    [AgentRuntimeStatus.Starting, AgentRuntimeStatus.WaitingForBrokerRegistration, AgentRuntimeStatus.Running, AgentRuntimeStatus.CompletionReported, AgentRuntimeStatus.Stopping];

    public async Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dueIds = await dbContext.AgentSchedules.AsNoTracking()
            .Where(x => x.IsEnabled && x.NextTickAt != null && x.NextTickAt <= now)
            .OrderBy(x => x.NextTickAt).Select(x => x.Id).Take(10).ToListAsync(cancellationToken);
        var processed = 0;
        foreach (var id in dueIds)
        {
            if (await ClaimAndQueueAsync(id, now, cancellationToken)) processed++;
        }
        return processed;
    }

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var changed = 0;
        var now = DateTimeOffset.UtcNow;
        var instances = await dbContext.AgentRuntimeInstances
            .Include(x => x.AgentInstallation)!.ThenInclude(x => x!.Schedule)
            .Include(x => x.AgentInstallation)!.ThenInclude(x => x!.Grant)
            .Include(x => x.AgentInstallation)!.ThenInclude(x => x!.PackageVersion)
            .Where(x => x.Status == AgentRuntimeStatus.Queued || ContainerActiveStatuses.Contains(x.Status))
            .OrderBy(x => x.QueuedAt).ToListAsync(cancellationToken);

        foreach (var instance in instances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (instance.Status == AgentRuntimeStatus.Queued)
            {
                if (await TryStartAsync(instance, now, cancellationToken)) changed++;
                continue;
            }
            if (instance.Status == AgentRuntimeStatus.CompletionReported)
            {
                await StopAndFinishAsync(instance, AgentRuntimeStatus.Completed, "Agent completion processed.", now, cancellationToken);
                changed++;
                continue;
            }
            if (instance.Status == AgentRuntimeStatus.WaitingForBrokerRegistration && instance.StartedAt?.AddSeconds((await SettingsAsync(cancellationToken)).BrokerRegistrationTimeoutSeconds) <= now)
            {
                await StopAndFinishAsync(instance, AgentRuntimeStatus.BrokerRegistrationTimedOut, "Broker registration timed out.", now, cancellationToken);
                changed++;
                continue;
            }
            if (instance.Status == AgentRuntimeStatus.Running && instance.RuntimeDeadlineAt <= now)
            {
                await StopAndFinishAsync(instance, AgentRuntimeStatus.RuntimeTimedOut, "Maximum runtime elapsed.", now, cancellationToken);
                changed++;
                continue;
            }
            if (instance.ContainerId is not null && instance.Status is AgentRuntimeStatus.WaitingForBrokerRegistration or AgentRuntimeStatus.Running)
            {
                var status = await containers.InspectAsync(instance.ContainerId, cancellationToken);
                if (status is null || status.State is AgentContainerState.Exited or AgentContainerState.Dead)
                {
                    var terminal = status?.ExitCode is 0 ? AgentRuntimeStatus.ExitedWithoutCompletion : AgentRuntimeStatus.Failed;
                    Transition(instance, terminal, now, status?.Error ?? "Container exited without a completion event.");
                    await dbContext.SaveChangesAsync(cancellationToken);
                    changed++;
                }
            }
        }
        return changed;
    }

    private async Task<bool> ClaimAndQueueAsync(Guid scheduleId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var schedule = await dbContext.AgentSchedules.Include(x => x.AgentInstallation)!.ThenInclude(x => x!.PackageVersion)
            .SingleOrDefaultAsync(x => x.Id == scheduleId, cancellationToken);
        if (schedule?.NextTickAt is null || schedule.NextTickAt > now || schedule.AgentInstallation?.IsEnabled != true) return false;
        var claimedTickAt = schedule.NextTickAt.Value;
        schedule.LastTickAt = now;
        schedule.RunRequestedAt = null;
        schedule.NextTickAt = schedule.ActivationMode switch
        {
            ActivationMode.Periodic => now.AddSeconds(schedule.TickFrequencySeconds),
            ActivationMode.AlwaysOn => null,
            _ => null
        };

        var active = await dbContext.AgentRuntimeInstances.Where(x => x.AgentInstallationId == schedule.AgentInstallationId && (x.Status == AgentRuntimeStatus.Queued || ContainerActiveStatuses.Contains(x.Status))).OrderBy(x => x.QueuedAt).ToListAsync(cancellationToken);
        if (active.Count > 0 && schedule.OverlapPolicy == OverlapPolicy.Skip)
        {
            AddTerminalInstance(schedule.AgentInstallationId, AgentRuntimeStatus.Skipped, now, "Skipped because a prior runtime is active.");
        }
        else
        {
            if (active.Count > 0 && schedule.OverlapPolicy == OverlapPolicy.CancelPrevious)
                foreach (var prior in active) await StopAndFinishAsync(prior, AgentRuntimeStatus.Cancelled, "Cancelled by overlap policy.", now, cancellationToken);
            var instance = new AgentRuntimeInstance { Id = Guid.NewGuid(), TickId = Guid.NewGuid(), AgentInstallationId = schedule.AgentInstallationId, QueuedAt = now };
            instance.Events.Add(new AgentRuntimeEvent { Id = Guid.NewGuid(), AgentRuntimeInstanceId = instance.Id, Status = AgentRuntimeStatus.Queued, Reason = $"Claimed schedule tick {claimedTickAt:O}.", OccurredAt = now });
            dbContext.AgentRuntimeInstances.Add(instance);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> TryStartAsync(AgentRuntimeInstance instance, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var installation = instance.AgentInstallation!;
        var settings = await SettingsAsync(cancellationToken);
        var globalCount = await dbContext.AgentRuntimeInstances.CountAsync(x => ContainerActiveStatuses.Contains(x.Status), cancellationToken);
        var businessCount = await dbContext.AgentRuntimeInstances.CountAsync(x => ContainerActiveStatuses.Contains(x.Status) && x.AgentInstallation!.BusinessId == installation.BusinessId, cancellationToken);
        var installationCount = await dbContext.AgentRuntimeInstances.CountAsync(x => ContainerActiveStatuses.Contains(x.Status) && x.AgentInstallationId == installation.Id, cancellationToken);
        if (globalCount >= settings.GlobalMaxActiveContainers || businessCount >= settings.PerBusinessMaxActiveContainers || installationCount >= settings.PerInstallationMaxActiveContainers)
            return false;
        var package = installation.PackageVersion!;
        if (!installation.IsEnabled || installation.Schedule?.IsEnabled != true || package.Status != AgentPackageVersionStatus.Built || string.IsNullOrWhiteSpace(package.PackagePath) || installation.Grant is null)
        {
            Transition(instance, AgentRuntimeStatus.PolicyDenied, now, "Installation is disabled or its package is not built.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        instance.WorkloadTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        instance.ContainerName = $"csweet-agent-{instance.Id:N}";
        Transition(instance, AgentRuntimeStatus.Starting, now, "Starting runtime container.");
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            var entryAssembly = Path.GetFileNameWithoutExtension(package.ProjectPath) + ".dll";
            var status = await containers.StartAsync(new AgentContainerStartRequest(
                instance.Id, instance.TickId, installation.Id, package.AgentId, installation.BusinessId,
                instance.ContainerName, settings.DotNetRuntimeBaseImage, package.PackagePath, entryAssembly,
                options.Value.BrokerEndpoint, token, "/app/csweet-agent.json", options.Value.DockerNetworkName,
                installation.Grant.MemoryMb, installation.Grant.CpuPercent, settings.DefaultContainerPidsLimit,
                installation.Schedule.MaxRuntimeSeconds), cancellationToken);
            instance.ContainerId = status.ContainerId;
            instance.RuntimeDeadlineAt = now.AddSeconds(installation.Schedule.MaxRuntimeSeconds);
            Transition(instance, AgentRuntimeStatus.WaitingForBrokerRegistration, DateTimeOffset.UtcNow, "Container started; awaiting broker registration.");
        }
        catch (Exception exception) when (exception is AgentContainerException or InvalidOperationException)
        {
            logger.LogError(exception, "Failed to start runtime {RuntimeInstanceId}", instance.Id);
            Transition(instance, AgentRuntimeStatus.StartFailed, DateTimeOffset.UtcNow, exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task StopAndFinishAsync(AgentRuntimeInstance instance, AgentRuntimeStatus terminal, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Transition(instance, AgentRuntimeStatus.Stopping, now, reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (instance.ContainerId is not null)
        {
            var settings = await SettingsAsync(cancellationToken);
            try
            {
                await containers.StopAsync(instance.ContainerId, TimeSpan.FromSeconds(settings.ContainerStopGraceSeconds), cancellationToken);
                if (settings.RemoveContainersAfterCompletion) await containers.RemoveAsync(instance.ContainerId, cancellationToken: cancellationToken);
            }
            catch (AgentContainerException exception) { logger.LogWarning(exception, "Container cleanup failed for runtime {RuntimeInstanceId}", instance.Id); }
        }
        Transition(instance, terminal, DateTimeOffset.UtcNow, reason);
        if (terminal == AgentRuntimeStatus.Completed && instance.AgentInstallation?.Schedule is { } schedule)
            schedule.LastCompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddTerminalInstance(Guid installationId, AgentRuntimeStatus status, DateTimeOffset now, string reason)
    {
        var instance = new AgentRuntimeInstance { Id = Guid.NewGuid(), TickId = Guid.NewGuid(), AgentInstallationId = installationId, QueuedAt = now };
        instance.TransitionTo(status, now, reason);
        instance.Events.Add(new AgentRuntimeEvent { Id = Guid.NewGuid(), AgentRuntimeInstanceId = instance.Id, Status = status, Reason = reason, OccurredAt = now });
        dbContext.AgentRuntimeInstances.Add(instance);
    }

    private static void Transition(AgentRuntimeInstance instance, AgentRuntimeStatus status, DateTimeOffset at, string reason)
        => AgentRuntimeSignalService.Transition(instance, status, at, reason);

    private async Task<AgentRuntimeGlobalSettings> SettingsAsync(CancellationToken cancellationToken)
        => await dbContext.AgentRuntimeGlobalSettings.SingleAsync(cancellationToken);
}
