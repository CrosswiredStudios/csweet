using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Contracts.Agents;
using CSweet.Domain.Communications;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CSweet.AgentHost.Broker;

/// <summary>
/// Reliably offers each durable onboarding event after its exact agent installation
/// has connected. Lifecycle delivery is platform-owned and therefore does not depend
/// on an optional subscription grant. The stable event is retried until acknowledged.
/// </summary>
public sealed class AgentOnboardingEventDispatcher(
    IServiceScopeFactory scopeFactory,
    AgentSessionRegistry sessions,
    TimeProvider clock,
    IOptions<AgentOnboardingDeliveryOptions> options,
    ILogger<AgentOnboardingEventDispatcher> logger) : BackgroundService
{
    private const string ConnectionUnavailableErrorPrefix = "The target agent installation is not connected yet.";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ConnectionRetryDelay = PollInterval;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, clock);
        do
        {
            await DispatchPendingAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task DispatchPendingAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        var now = clock.GetUtcNow();
        var pending = await db.AgentOnboardingEventOutbox
            .Where(x =>
                (x.Status == AgentOnboardingEventOutboxStatus.Pending && x.NextAttemptAt <= now) ||
                (x.Status == AgentOnboardingEventOutboxStatus.Failed &&
                 x.LastError != null &&
                 x.LastError.StartsWith(ConnectionUnavailableErrorPrefix)))
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pending.Count > 0)
        {
            logger.LogInformation(
                "Found {PendingOnboardingEventCount} agent onboarding event(s) ready for delivery at {DispatchTime}.",
                pending.Count,
                now);
        }

        foreach (var item in pending)
        {
            var agent = await db.CoreOrganizationUsers.AsNoTracking()
                .Where(x => x.Id == item.AgentOrganizationUserId && x.OrganizationId == item.OrganizationId)
                .Select(x => new { x.IsActive, x.AgentInstallationId, x.DisplayName })
                .SingleOrDefaultAsync(cancellationToken);
            if (agent is null || !agent.IsActive || !agent.AgentInstallationId.HasValue)
            {
                item.Status = AgentOnboardingEventOutboxStatus.Cancelled;
                item.LastError = "The agent employee is no longer active or assigned to an installation.";
                logger.LogWarning(
                    "Cancelled onboarding event {OnboardingEventId} for organization {OrganizationId} and employee {AgentOrganizationUserId}: {OnboardingError}",
                    item.Id,
                    item.OrganizationId,
                    item.AgentOrganizationUserId,
                    item.LastError);
                continue;
            }

            var recoveringLegacyConnectionWait =
                WasWaitingForConnection(item.LastError) &&
                (item.Attempts > 0 || item.Status == AgentOnboardingEventOutboxStatus.Failed);
            if (recoveringLegacyConnectionWait)
            {
                var schedule = await db.AgentSchedules.SingleOrDefaultAsync(
                    x => x.AgentInstallationId == agent.AgentInstallationId.Value,
                    cancellationToken);
                if (schedule?.AutomaticStartSuppressedAt is not null)
                {
                    schedule.ConsecutiveStartupFailures = 0;
                    schedule.AutomaticStartSuppressedAt = null;
                    logger.LogInformation(
                        "Cleared automatic startup suppression for installation {InstallationId} while recovering onboarding event {OnboardingEventId}; always-on reconciliation can start the corrected runtime.",
                        agent.AgentInstallationId,
                        item.Id);
                }
            }

            // Older dispatcher versions counted polls made while the installation was
            // disconnected as delivery attempts. Recover those counters so an event that
            // the agent never received still gets the full acknowledgement retry budget.
            if (WasWaitingForConnection(item.LastError))
            {
                if (item.Status == AgentOnboardingEventOutboxStatus.Failed)
                {
                    logger.LogInformation(
                        "Reopened onboarding event {OnboardingEventId} for organization {OrganizationId}, employee {AgentOrganizationUserId}, and installation {InstallationId} because an older dispatcher exhausted retries before the installation connected.",
                        item.Id,
                        item.OrganizationId,
                        item.AgentOrganizationUserId,
                        agent.AgentInstallationId);
                    item.Status = AgentOnboardingEventOutboxStatus.Pending;
                }
                item.Attempts = 0;
            }

            if (item.Attempts >= options.Value.MaximumAttempts)
            {
                FailPermanently(db, item, agent.DisplayName, agent.AgentInstallationId.Value, now);
                logger.LogWarning(
                    "Stopped onboarding event {EventId} for agent employee {AgentOrganizationUserId} after {AttemptCount} attempts. {LastError}",
                    item.Id, item.AgentOrganizationUserId, item.Attempts, item.LastError);
                continue;
            }

            string? configurationWarning = null;
            var configuration = await db.AgentInstallationConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.AgentInstallationId == agent.AgentInstallationId.Value,
                    cancellationToken);
            if (configuration is not null)
            {
                IReadOnlyDictionary<string, JsonElement>? settings = null;
                try
                {
                    settings = JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(
                        configuration.SettingsJson,
                        JsonOptions) ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                }
                catch (JsonException exception)
                {
                    configurationWarning = "The saved agent configuration is not valid JSON.";
                    logger.LogWarning(
                        exception,
                        "Could not deserialize saved configuration for onboarding event {OnboardingEventId} and installation {InstallationId}. The lifecycle event will still be offered.",
                        item.Id,
                        agent.AgentInstallationId);
                }

                if (settings is not null)
                {
                    var update = new UpdateAgentConfigurationRequest(settings)
                    {
                        SchemaVersion = configuration.SchemaVersion
                    };
                    using var hydrationTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    hydrationTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                    CapabilityResult hydration;
                    try
                    {
                        hydration = await sessions.InvokeInstallationCapabilityAsync(
                            item.OrganizationId.ToString("D"),
                            agent.AgentInstallationId.Value.ToString("D"),
                            new RequestCapability
                            {
                                RequestId = Guid.NewGuid().ToString("N"),
                                Capability = AgentConfigurationCapabilities.Update,
                                ContentType = "application/json",
                                Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(update, JsonOptions))
                            },
                            hydrationTimeout.Token);
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        hydration = new CapabilityResult
                        {
                            Succeeded = false,
                            Error = "The agent did not accept its saved configuration before onboarding."
                        };
                    }

                    if (!hydration.Succeeded)
                    {
                        configurationWarning = string.IsNullOrWhiteSpace(hydration.Error)
                            ? "The agent rejected its saved configuration before onboarding."
                            : $"The agent could not load its saved configuration before onboarding: {hydration.Error}";
                        logger.LogWarning(
                            "Installation {InstallationId} could not load saved configuration before onboarding event {EventId}. The lifecycle event will still be offered so the agent can use its built-in or contextual fallback. {Error}",
                            agent.AgentInstallationId,
                            item.Id,
                            configurationWarning);
                    }
                }
            }

            var payload = new AgentOnboardedEvent(
                item.OrganizationId,
                item.AgentOrganizationUserId,
                item.HiringOrganizationUserId,
                item.ConversationId,
                item.OccurredAt);
            var delivered = sessions.PublishPlatformEvent(
                item.OrganizationId.ToString("D"),
                AgentLifecycleEvents.Onboarded,
                $"organization/{item.OrganizationId:D}/agent/{item.AgentOrganizationUserId:D}/onboarding",
                ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions)),
                item.Id.ToString("N"),
                agent.AgentInstallationId.Value.ToString("D"),
                eventId: item.Id.ToString("N"),
                requireSubscription: false,
                occurredAt: item.OccurredAt);

            if (delivered > 0)
            {
                item.Attempts++;
                item.NextAttemptAt = now + TimeSpan.FromSeconds(30);
                item.LastError = "The agent received the onboarding event but has not acknowledged successful handling.";
                logger.LogInformation(
                    "Offered onboarding event {EventId} to agent employee {AgentOrganizationUserId} installation {InstallationId}; awaiting acknowledgement.",
                    item.Id, item.AgentOrganizationUserId, agent.AgentInstallationId);
            }
            else
            {
                // Runtime startup and onboarding delivery intentionally race. Retry on
                // the next dispatcher poll once the container has had a chance to
                // register instead of making a healthy first launch wait 15 seconds.
                item.NextAttemptAt = now + ConnectionRetryDelay;
                item.LastError = configurationWarning is null
                    ? ConnectionUnavailableErrorPrefix
                    : $"{ConnectionUnavailableErrorPrefix} {configurationWarning}";
                logger.LogWarning(
                    "Could not offer onboarding event {OnboardingEventId} for organization {OrganizationId}, employee {AgentOrganizationUserId}, and installation {InstallationId} because the installation is not connected. This does not consume an acknowledgement attempt. Next retry: {NextAttemptAt}. {OnboardingError}",
                    item.Id,
                    item.OrganizationId,
                    item.AgentOrganizationUserId,
                    agent.AgentInstallationId,
                    item.NextAttemptAt,
                    item.LastError);
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(cancellationToken);
    }

    private static void FailPermanently(
        CSweetDbContext db,
        AgentOnboardingEventOutboxItem item,
        string agentDisplayName,
        Guid installationId,
        DateTimeOffset now)
    {
        item.Status = AgentOnboardingEventOutboxStatus.Failed;
        var lastError = item.LastError ?? "The agent did not complete the onboarding lifecycle event.";
        db.UserNotifications.Add(new UserNotification
        {
            Id = Guid.NewGuid(),
            OrganizationId = item.OrganizationId,
            RecipientOrganizationUserId = item.HiringOrganizationUserId,
            OriginatingAgentOrganizationUserId = item.AgentOrganizationUserId,
            Severity = NotificationSeverity.Important,
            Category = "AgentOnboarding",
            Title = $"{agentDisplayName} onboarding needs attention",
            Body = $"C-Sweet stopped retrying onboarding after {item.Attempts} attempts. Last issue: {lastError} Agent employee: {agentDisplayName} ({item.AgentOrganizationUserId:D}). Installation: {installationId:D}. Lifecycle event: {item.Id:D}.",
            ActionUri = $"/organizations/{item.OrganizationId:D}/employees",
            DeduplicationKey = $"agent-onboarding-failed:{item.Id:D}",
            CreatedAt = now
        });
    }

    private static bool WasWaitingForConnection(string? lastError) =>
        lastError?.StartsWith(ConnectionUnavailableErrorPrefix, StringComparison.Ordinal) == true;
}
