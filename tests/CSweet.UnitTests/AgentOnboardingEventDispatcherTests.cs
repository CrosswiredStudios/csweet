using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using CSweet.Contracts.Agents;
using CSweet.Domain.Communications;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CSweet.UnitTests;

public sealed class AgentOnboardingEventDispatcherTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DispatchAndAcknowledge_TargetExactInstallationAndCompleteDurableEvent()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var otherInstallationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            var organization = new Organization { Id = organizationId, Name = "Example", Status = OrganizationStatus.Active,
                CreatedAt = occurredAt, UpdatedAt = occurredAt };
            var owner = new OrganizationUser { Id = ownerId, OrganizationId = organizationId, DisplayName = "Owner",
                EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Owner, IsActive = true, CreatedAt = occurredAt };
            var agent = new OrganizationUser { Id = agentId, OrganizationId = organizationId, AgentInstallationId = installationId,
                DisplayName = "Chief", EmployeeType = EmployeeType.Agent, PermissionLevel = OrganizationPermissionLevel.Manager,
                IsActive = true, CreatedAt = occurredAt };
            var conversation = new Conversation { Id = conversationId, OrganizationId = organizationId,
                InitiatedByOrganizationUserId = ownerId, AgentOrganizationUserId = agentId,
                Kind = ConversationKind.DirectHumanAgent, IsPrivate = true, IsDeletionProtected = true,
                CreatedAt = occurredAt, UpdatedAt = occurredAt };
            db.AddRange(organization, owner, agent, conversation, new AgentInstallationConfiguration
            {
                Id = Guid.NewGuid(),
                AgentInstallationId = installationId,
                SchemaVersion = "1.0",
                SettingsJson = "{\"llmProviderId\":\"11111111-1111-1111-1111-111111111111\",\"llmModel\":\"local-model\"}",
                CreatedAt = occurredAt,
                UpdatedAt = occurredAt
            }, new AgentOnboardingEventOutboxItem
            {
                Id = eventId, OrganizationId = organizationId, AgentOrganizationUserId = agentId,
                HiringOrganizationUserId = ownerId, ConversationId = conversationId,
                Status = AgentOnboardingEventOutboxStatus.Pending, NextAttemptAt = occurredAt, OccurredAt = occurredAt
            });
            await db.SaveChangesAsync();
        }

        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var target = Register(registry, organizationId, installationId, AgentConfigurationCapabilities.Update);
        var sibling = Register(registry, organizationId, otherInstallationId);
        var dispatcher = new AgentOnboardingEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(), registry,
            TimeProvider.System, Options.Create(new AgentOnboardingDeliveryOptions { MaximumAttempts = 3 }),
            NullLogger<AgentOnboardingEventDispatcher>.Instance);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var dispatch = dispatcher.DispatchPendingAsync(CancellationToken.None);
        var configuration = await target.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(AgentConfigurationCapabilities.Update, configuration.CapabilityRequest.Capability);
        registry.CompleteCapability(target, new CapabilityResult
        {
            RequestId = configuration.CapabilityRequest.RequestId,
            Succeeded = true,
            ContentType = "application/json",
            Payload = ByteString.CopyFromUtf8("{\"succeeded\":true,\"settings\":{}}")
        }, configuration.CorrelationId);
        await dispatch;

        var delivered = await target.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(eventId.ToString("N"), delivered.Event.EventId);
        Assert.Equal(AgentLifecycleEvents.Onboarded, delivered.Event.EventType);
        Assert.False(sibling.Outbound.TryRead(out _));
        var payload = JsonSerializer.Deserialize<AgentOnboardedEvent>(delivered.Event.Payload.Span, JsonOptions);
        Assert.Equal(conversationId, payload!.ConversationId);

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            var offered = await db.AgentOnboardingEventOutbox.SingleAsync();
            Assert.Equal(AgentOnboardingEventOutboxStatus.Pending, offered.Status);
            Assert.Equal(1, offered.Attempts);

            var handler = new AgentOnboardingCapabilityHandler(db, TimeProvider.System);
            var denied = await InvokeAsync(handler, sibling, Acknowledge(eventId));
            var acknowledged = await InvokeAsync(handler, target, Acknowledge(eventId));
            Assert.False(denied.Succeeded);
            Assert.True(acknowledged.Succeeded);
            Assert.Equal(AgentOnboardingEventOutboxStatus.Delivered, (await db.AgentOnboardingEventOutbox.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task Dispatch_WhenSavedConfigurationCannotBeHydrated_StillOffersOnboardingEvent()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            db.AddRange(
                new OrganizationUser
                {
                    Id = agentId,
                    OrganizationId = organizationId,
                    AgentInstallationId = installationId,
                    DisplayName = "Chief",
                    EmployeeType = EmployeeType.Agent,
                    PermissionLevel = OrganizationPermissionLevel.Manager,
                    IsActive = true,
                    CreatedAt = occurredAt
                },
                new AgentInstallationConfiguration
                {
                    Id = Guid.NewGuid(),
                    AgentInstallationId = installationId,
                    SchemaVersion = "1.0",
                    SettingsJson = "{\"llmProviderId\":\"11111111-1111-1111-1111-111111111111\"}",
                    CreatedAt = occurredAt,
                    UpdatedAt = occurredAt
                },
                new AgentOnboardingEventOutboxItem
                {
                    Id = eventId,
                    OrganizationId = organizationId,
                    AgentOrganizationUserId = agentId,
                    HiringOrganizationUserId = ownerId,
                    ConversationId = conversationId,
                    Status = AgentOnboardingEventOutboxStatus.Pending,
                    NextAttemptAt = occurredAt,
                    OccurredAt = occurredAt
                });
            await db.SaveChangesAsync();
        }

        var registry = new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance);
        var target = Register(registry, organizationId, installationId);
        var dispatcher = new AgentOnboardingEventDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            registry,
            TimeProvider.System,
            Options.Create(new AgentOnboardingDeliveryOptions { MaximumAttempts = 3 }),
            NullLogger<AgentOnboardingEventDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var delivered = await target.Outbound.ReadAsync(timeout.Token);
        Assert.Equal(AgentLifecycleEvents.Onboarded, delivered.Event.EventType);
        Assert.Equal(eventId.ToString("N"), delivered.Event.EventId);
        await using var verificationScope = provider.CreateAsyncScope();
        var item = await verificationScope.ServiceProvider.GetRequiredService<CSweetDbContext>()
            .AgentOnboardingEventOutbox.SingleAsync();
        Assert.Equal(1, item.Attempts);
        Assert.Contains("has not acknowledged", item.LastError);
    }

    [Fact]
    public async Task Dispatch_WhenLegacyFailureOnlyWaitedForConnection_ReopensWithoutConsumingDeliveryAttempts()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            db.AddRange(
                new OrganizationUser
                {
                    Id = agentId,
                    OrganizationId = organizationId,
                    AgentInstallationId = installationId,
                    DisplayName = "Chief",
                    EmployeeType = EmployeeType.Agent,
                    PermissionLevel = OrganizationPermissionLevel.Manager,
                    IsActive = true,
                    CreatedAt = occurredAt
                },
                new AgentOnboardingEventOutboxItem
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    AgentOrganizationUserId = agentId,
                    HiringOrganizationUserId = Guid.NewGuid(),
                    ConversationId = Guid.NewGuid(),
                    Status = AgentOnboardingEventOutboxStatus.Failed,
                    Attempts = 3,
                    NextAttemptAt = occurredAt,
                    OccurredAt = occurredAt,
                    LastError = "The target agent installation is not connected yet."
                },
                new AgentSchedule
                {
                    Id = Guid.NewGuid(),
                    AgentInstallationId = installationId,
                    ActivationMode = ActivationMode.AlwaysOn,
                    TickFrequencySeconds = 60,
                    MaxRuntimeSeconds = 300,
                    ConsecutiveStartupFailures = 5,
                    AutomaticStartSuppressedAt = occurredAt,
                    OverlapPolicy = OverlapPolicy.Skip,
                    IsEnabled = true
                });
            await db.SaveChangesAsync();
        }

        var dispatcher = new AgentOnboardingEventDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance),
            TimeProvider.System,
            Options.Create(new AgentOnboardingDeliveryOptions { MaximumAttempts = 3 }),
            NullLogger<AgentOnboardingEventDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        await using var verificationScope = provider.CreateAsyncScope();
        var item = await verificationScope.ServiceProvider.GetRequiredService<CSweetDbContext>()
            .AgentOnboardingEventOutbox.SingleAsync();
        Assert.Equal(AgentOnboardingEventOutboxStatus.Pending, item.Status);
        Assert.Equal(0, item.Attempts);
        Assert.True(item.NextAttemptAt > DateTimeOffset.UtcNow);
        Assert.Contains("not connected", item.LastError);
        var schedule = await verificationScope.ServiceProvider.GetRequiredService<CSweetDbContext>()
            .AgentSchedules.SingleAsync();
        Assert.Equal(0, schedule.ConsecutiveStartupFailures);
        Assert.Null(schedule.AutomaticStartSuppressedAt);
    }

    [Fact]
    public async Task ExhaustedAttempts_FailsEventAndCreatesRealtimeHiringUserNotification()
    {
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString("N");
        services.AddDbContext<CSweetDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
            var organization = new Organization { Id = organizationId, Name = "Example", Status = OrganizationStatus.Active,
                CreatedAt = occurredAt, UpdatedAt = occurredAt };
            var owner = new OrganizationUser { Id = ownerId, OrganizationId = organizationId, DisplayName = "Owner",
                EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Owner, IsActive = true, CreatedAt = occurredAt };
            var agent = new OrganizationUser { Id = agentId, OrganizationId = organizationId, AgentInstallationId = installationId,
                DisplayName = "Unreliable Programmer", EmployeeType = EmployeeType.Agent,
                PermissionLevel = OrganizationPermissionLevel.Contributor, IsActive = true, CreatedAt = occurredAt };
            var conversation = new Conversation { Id = conversationId, OrganizationId = organizationId,
                InitiatedByOrganizationUserId = ownerId, AgentOrganizationUserId = agentId,
                Kind = ConversationKind.DirectHumanAgent, IsPrivate = true, IsDeletionProtected = true,
                CreatedAt = occurredAt, UpdatedAt = occurredAt };
            db.AddRange(organization, owner, agent, conversation, new AgentOnboardingEventOutboxItem
            {
                Id = eventId, OrganizationId = organizationId, AgentOrganizationUserId = agentId,
                HiringOrganizationUserId = ownerId, ConversationId = conversationId,
                Status = AgentOnboardingEventOutboxStatus.Pending, Attempts = 2,
                NextAttemptAt = occurredAt, OccurredAt = occurredAt,
                LastError = "The agent received the event but did not acknowledge it."
            });
            await db.SaveChangesAsync();
        }

        var dispatcher = new AgentOnboardingEventDispatcher(provider.GetRequiredService<IServiceScopeFactory>(),
            new AgentSessionRegistry(NullLogger<AgentSessionRegistry>.Instance), TimeProvider.System,
            Options.Create(new AgentOnboardingDeliveryOptions { MaximumAttempts = 2 }),
            NullLogger<AgentOnboardingEventDispatcher>.Instance);

        await dispatcher.DispatchPendingAsync(CancellationToken.None);

        await using var verificationScope = provider.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CSweetDbContext>();
        Assert.Equal(AgentOnboardingEventOutboxStatus.Failed,
            (await verificationDb.AgentOnboardingEventOutbox.SingleAsync()).Status);
        var notification = await verificationDb.UserNotifications.SingleAsync();
        Assert.Equal(ownerId, notification.RecipientOrganizationUserId);
        Assert.Equal(NotificationSeverity.Important, notification.Severity);
        Assert.Contains("Unreliable Programmer", notification.Body);
        Assert.Contains(installationId.ToString("D"), notification.Body);
        Assert.Contains("did not acknowledge", notification.Body);
        Assert.Contains(await verificationDb.ApplicationRealtimeOutbox.ToListAsync(),
            x => x.EventType == CSweet.Contracts.Realtime.AppRealtimeEvents.NotificationCreated);
    }

    private static AgentSession Register(
        AgentSessionRegistry registry,
        Guid organizationId,
        Guid installationId,
        params string[] capabilities) =>
        registry.Register(new RegisterAgent
        {
            AgentId = "chief", AgentVersion = "1.0.0", BusinessId = organizationId.ToString("D"),
            InstallationId = installationId.ToString("D")
        }, new AuthorizedAgentGrant(capabilities.ToHashSet(StringComparer.Ordinal), new HashSet<string>(), new HashSet<string>(),
            new HashSet<string>(), new HashSet<string>()));

    private static RequestCapability Acknowledge(Guid eventId) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"),
        Capability = AgentLifecycleCapabilities.CompleteOnboarding,
        ContentType = "application/json",
        Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(new CompleteAgentOnboardingRequest(eventId), JsonOptions))
    };

    private static async Task<CapabilityResult> InvokeAsync(
        AgentOnboardingCapabilityHandler handler,
        AgentSession session,
        RequestCapability request)
    {
        await foreach (var result in handler.HandleAsync(session, request, CancellationToken.None)) return result;
        throw new InvalidOperationException("Handler returned no result.");
    }
}
