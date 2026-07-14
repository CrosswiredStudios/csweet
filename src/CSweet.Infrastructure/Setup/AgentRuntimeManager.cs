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
    IAuditEventWriter auditWriter,
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
            .OrderBy(x => x.NextTickAt).Select(x => x.Id)
            .Take(Math.Clamp(options.Value.MaximumScheduleClaimsPerIteration, 1, 100))
            .ToListAsync(cancellationToken);
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
            if (instance.Status != AgentRuntimeStatus.Queued &&
                (instance.AgentInstallation?.IsEnabled != true || instance.AgentInstallation.Schedule?.IsEnabled != true))
            {
                await StopAndFinishAsync(instance, AgentRuntimeStatus.Cancelled, "Installation or schedule was disabled.", now, cancellationToken);
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
                    await StopAndFinishAsync(instance, terminal, status?.Error ?? "Container exited without a completion event.", now, cancellationToken);
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
        var cancelPrevious = new List<AgentRuntimeInstance>();
        var tickOutcome = "queued";
        if (active.Count > 0 && schedule.OverlapPolicy == OverlapPolicy.Skip)
        {
            tickOutcome = "skipped";
            AddTerminalInstance(schedule.AgentInstallationId, AgentRuntimeStatus.Skipped, now, "Skipped because a prior runtime is active.");
        }
        else
        {
            if (active.Count > 0 && schedule.OverlapPolicy == OverlapPolicy.CancelPrevious)
                cancelPrevious.AddRange(active);
            var instance = new AgentRuntimeInstance { Id = Guid.NewGuid(), TickId = Guid.NewGuid(), AgentInstallationId = schedule.AgentInstallationId, QueuedAt = now };
            instance.Events.Add(new AgentRuntimeEvent { Id = Guid.NewGuid(), AgentRuntimeInstanceId = instance.Id, Status = AgentRuntimeStatus.Queued, Reason = $"Claimed schedule tick {claimedTickAt:O}.", OccurredAt = now });
            dbContext.AgentRuntimeInstances.Add(instance);
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            AgentRuntimeMetrics.Tick(schedule.ActivationMode.ToString(), tickOutcome);
            await auditWriter.WriteAsync("agent-runtime.schedule.tick", nameof(AgentSchedule), schedule.Id,
                $"Schedule tick {tickOutcome} for installation {schedule.AgentInstallationId}.", cancellationToken: cancellationToken);
            foreach (var prior in cancelPrevious)
                await StopAndFinishAsync(prior, AgentRuntimeStatus.Cancelled, "Cancelled by overlap policy.", now, cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogDebug("Schedule {ScheduleId} was claimed by another worker.", scheduleId);
            dbContext.ChangeTracker.Clear();
            return false;
        }
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
            await AuditOutcomeAsync(instance, AgentRuntimeStatus.PolicyDenied, cancellationToken);
            return true;
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        instance.WorkloadTokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        instance.ContainerName = $"csweet-agent-{instance.Id:N}";
        Transition(instance, AgentRuntimeStatus.Starting, now, "Starting runtime container.");
        await dbContext.SaveChangesAsync(cancellationToken);
        Transition(instance, AgentRuntimeStatus.WaitingForBrokerRegistration, DateTimeOffset.UtcNow, "Container launch authorized; awaiting broker registration.");
        await dbContext.SaveChangesAsync(cancellationToken);
        try
        {
            using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            startTimeout.CancelAfter(TimeSpan.FromSeconds(settings.ContainerStartTimeoutSeconds));
            var entryAssembly = Path.GetFileNameWithoutExtension(package.ProjectPath) + ".dll";
            var status = await containers.StartAsync(new AgentContainerStartRequest(
                instance.Id, instance.TickId, installation.Id, package.AgentId, installation.BusinessId,
                instance.ContainerName, settings.DotNetRuntimeBaseImage, package.PackagePath, entryAssembly,
                options.Value.BrokerEndpoint, token, "/app/csweet-agent.json", options.Value.DockerNetworkName,
                installation.Grant.MemoryMb, installation.Grant.CpuPercent, settings.DefaultContainerPidsLimit,
                installation.Schedule.MaxRuntimeSeconds), startTimeout.Token);
            instance.ContainerId = status.ContainerId;
            instance.RuntimeDeadlineAt = now.AddSeconds(installation.Schedule.MaxRuntimeSeconds);
            AgentRuntimeMetrics.ContainerStarted();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TryRemoveFailedStartAsync(instance.ContainerName, cancellationToken);
            Transition(instance, AgentRuntimeStatus.StartFailed, DateTimeOffset.UtcNow, "Container start timed out.");
        }
        catch (Exception exception) when (exception is AgentContainerException or InvalidOperationException)
        {
            logger.LogError(exception, "Failed to start runtime {RuntimeInstanceId}", instance.Id);
            await TryRemoveFailedStartAsync(instance.ContainerName, cancellationToken);
            Transition(instance, AgentRuntimeStatus.StartFailed, DateTimeOffset.UtcNow, exception.Message);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        if (instance.Status == AgentRuntimeStatus.WaitingForBrokerRegistration)
            await auditWriter.WriteAsync("agent-runtime.container.started", nameof(AgentRuntimeInstance), instance.Id,
                $"Started container {instance.ContainerId} for installation {instance.AgentInstallationId}.", cancellationToken: cancellationToken);
        else if (instance.Status == AgentRuntimeStatus.StartFailed)
            await AuditOutcomeAsync(instance, AgentRuntimeStatus.StartFailed, cancellationToken);
        return true;
    }

    private async Task TryRemoveFailedStartAsync(string containerName, CancellationToken cancellationToken)
    {
        try { await containers.RemoveAsync(containerName, force: true, cancellationToken: cancellationToken); }
        catch (AgentContainerException exception) { logger.LogDebug(exception, "No failed-start container remained for {ContainerName}.", containerName); }
    }

    private async Task StopAndFinishAsync(AgentRuntimeInstance instance, AgentRuntimeStatus terminal, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Transition(instance, AgentRuntimeStatus.Stopping, now, reason);
        await dbContext.SaveChangesAsync(cancellationToken);
        var settings = await SettingsAsync(cancellationToken);
        if (instance.ContainerId is not null)
        {
            var containerId = instance.ContainerId;
            try
            {
                var maximumLogBytes = Math.Min(settings.DefaultContainerLogLimitMb * 1024 * 1024, 64 * 1024);
                instance.LogExcerpt = await containers.GetLogsAsync(containerId, maximumLogBytes, cancellationToken);
            }
            catch (AgentContainerException exception)
            {
                logger.LogWarning(exception, "Could not retain logs for runtime {RuntimeInstanceId}.", instance.Id);
            }
            try
            {
                await containers.StopAsync(containerId, TimeSpan.FromSeconds(settings.ContainerStopGraceSeconds), cancellationToken);
                if (settings.RemoveContainersAfterCompletion)
                {
                    await containers.RemoveAsync(containerId, cancellationToken: cancellationToken);
                    instance.ContainerId = null;
                }
                AgentRuntimeMetrics.ContainerStopped(terminal.ToString());
                await auditWriter.WriteAsync("agent-runtime.container.stopped", nameof(AgentRuntimeInstance), instance.Id,
                    $"Stopped container {containerId}: {reason}", cancellationToken: cancellationToken);
            }
            catch (AgentContainerException exception) { logger.LogWarning(exception, "Container cleanup failed for runtime {RuntimeInstanceId}", instance.Id); }
        }
        Transition(instance, terminal, DateTimeOffset.UtcNow, reason);
        if (terminal == AgentRuntimeStatus.Completed && instance.AgentInstallation?.Schedule is { } schedule)
            schedule.LastCompletedAt = DateTimeOffset.UtcNow;
        ScheduleAlwaysOnRestart(instance, terminal, settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        await AuditOutcomeAsync(instance, terminal, cancellationToken);
    }

    private void AddTerminalInstance(Guid installationId, AgentRuntimeStatus status, DateTimeOffset now, string reason)
    {
        var instance = new AgentRuntimeInstance { Id = Guid.NewGuid(), TickId = Guid.NewGuid(), AgentInstallationId = installationId, QueuedAt = now };
        instance.TransitionTo(status, now, reason);
        instance.Events.Add(new AgentRuntimeEvent { Id = Guid.NewGuid(), AgentRuntimeInstanceId = instance.Id, Status = status, Reason = reason, OccurredAt = now });
        dbContext.AgentRuntimeInstances.Add(instance);
    }

    private void Transition(AgentRuntimeInstance instance, AgentRuntimeStatus status, DateTimeOffset at, string reason)
    {
        instance.TransitionTo(status, at, reason);
        dbContext.AgentRuntimeEvents.Add(new AgentRuntimeEvent { Id = Guid.NewGuid(), AgentRuntimeInstanceId = instance.Id, Status = status, Reason = reason, OccurredAt = at });
        logger.LogInformation("Agent runtime {RuntimeInstanceId} transitioned to {RuntimeStatus}: {Reason}", instance.Id, status, reason);
        if (AgentRuntimeInstance.IsTerminal(status))
            AgentRuntimeMetrics.RuntimeOutcome(status, instance.StartedAt is { } started ? at - started : null);
    }

    private Task AuditOutcomeAsync(AgentRuntimeInstance instance, AgentRuntimeStatus status, CancellationToken cancellationToken)
    {
        var eventType = status switch
        {
            AgentRuntimeStatus.Completed => "agent-runtime.completed",
            AgentRuntimeStatus.RuntimeTimedOut or AgentRuntimeStatus.BrokerRegistrationTimedOut => "agent-runtime.timeout",
            AgentRuntimeStatus.PolicyDenied => "agent-runtime.policy-denied",
            AgentRuntimeStatus.StartFailed or AgentRuntimeStatus.Failed or AgentRuntimeStatus.ExitedWithoutCompletion => "agent-runtime.failed",
            AgentRuntimeStatus.Cancelled => "agent-runtime.cancelled",
            _ => "agent-runtime.outcome"
        };
        return auditWriter.WriteAsync(eventType, nameof(AgentRuntimeInstance), instance.Id,
            $"Runtime ended as {status}: {instance.Reason}", cancellationToken: cancellationToken);
    }

    private static void ScheduleAlwaysOnRestart(AgentRuntimeInstance instance, AgentRuntimeStatus terminal, AgentRuntimeGlobalSettings settings)
    {
        var schedule = instance.AgentInstallation?.Schedule;
        if (schedule?.ActivationMode != ActivationMode.AlwaysOn || !schedule.IsEnabled || instance.AgentInstallation?.IsEnabled != true)
            return;
        var failed = terminal is not (AgentRuntimeStatus.Completed or AgentRuntimeStatus.Cancelled);
        if (settings.DefaultRestartPolicy == RestartPolicy.Always ||
            (settings.DefaultRestartPolicy == RestartPolicy.OnFailure && failed))
            schedule.NextTickAt = DateTimeOffset.UtcNow;
    }

    private async Task<AgentRuntimeGlobalSettings> SettingsAsync(CancellationToken cancellationToken)
        => await dbContext.AgentRuntimeGlobalSettings.SingleAsync(cancellationToken);
}
