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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
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
            .Where(x => x.Status == AgentOnboardingEventOutboxStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(cancellationToken);

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
                continue;
            }

            if (item.Attempts >= options.Value.MaximumAttempts)
            {
                FailPermanently(db, item, agent.DisplayName, agent.AgentInstallationId.Value, now);
                logger.LogWarning(
                    "Stopped onboarding event {EventId} for agent employee {AgentOrganizationUserId} after {AttemptCount} attempts. {LastError}",
                    item.Id, item.AgentOrganizationUserId, item.Attempts, item.LastError);
                continue;
            }

            var configuration = await db.AgentInstallationConfigurations.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.AgentInstallationId == agent.AgentInstallationId.Value,
                    cancellationToken);
            if (configuration is not null)
            {
                var settings = JsonSerializer.Deserialize<IReadOnlyDictionary<string, JsonElement>>(
                    configuration.SettingsJson,
                    JsonOptions) ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
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
                    item.Attempts++;
                    item.NextAttemptAt = now + RetryDelay(item.Attempts);
                    item.LastError = string.IsNullOrWhiteSpace(hydration.Error)
                        ? "The agent rejected its saved configuration before onboarding."
                        : $"The agent could not load its saved configuration before onboarding: {hydration.Error}";
                    logger.LogWarning(
                        "Deferred onboarding event {EventId} until installation {InstallationId} accepts its saved configuration. {Error}",
                        item.Id,
                        agent.AgentInstallationId,
                        item.LastError);
                    continue;
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

            item.Attempts++;
            if (delivered > 0)
            {
                item.NextAttemptAt = now + TimeSpan.FromSeconds(30);
                item.LastError = "The agent received the onboarding event but has not acknowledged successful handling.";
                logger.LogInformation(
                    "Offered onboarding event {EventId} to agent employee {AgentOrganizationUserId} installation {InstallationId}; awaiting acknowledgement.",
                    item.Id, item.AgentOrganizationUserId, agent.AgentInstallationId);
            }
            else
            {
                item.NextAttemptAt = now + RetryDelay(item.Attempts);
                item.LastError = "The target agent installation is not connected yet.";
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

    private static TimeSpan RetryDelay(int attempts) => attempts switch
    {
        <= 1 => TimeSpan.FromSeconds(5),
        <= 5 => TimeSpan.FromSeconds(15),
        <= 8 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(1)
    };
}
