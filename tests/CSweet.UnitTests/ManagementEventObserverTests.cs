using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AgentHost.Broker;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class ManagementEventObserverTests
{
    [Fact]
    public async Task ExecutiveReport_CreatesOneMarkdownMessageAndNotification()
    {
        await using var db = CreateDb();
        var organizationId = Guid.NewGuid();
        var installationId = Guid.NewGuid();
        var owner = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, DisplayName = "Owner",
            EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Owner, CreatedAt = DateTimeOffset.UtcNow };
        var chief = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organizationId, DisplayName = "Chief",
            EmployeeType = EmployeeType.Agent, PermissionLevel = OrganizationPermissionLevel.Manager, AgentInstallationId = installationId,
            ReportsToOrganizationUserId = owner.Id, CreatedAt = DateTimeOffset.UtcNow };
        var cycle = new ManagementCycle { Id = Guid.NewGuid(), OrganizationId = organizationId };
        var request = new ManagementCheckInRequestRecord { Id = Guid.NewGuid(), OrganizationId = organizationId,
            ManagementCycleId = cycle.Id, RequestedByOrganizationUserId = owner.Id, RequestedFromOrganizationUserId = chief.Id,
            CheckInType = "ExecutiveBriefing", TriggerType = "Manual", Status = "AwaitingReport", CreatedAt = DateTimeOffset.UtcNow,
            DueAt = DateTimeOffset.UtcNow.AddHours(2) };
        db.AddRange(new Organization { Id = organizationId, Name = "Example", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow },
            owner, chief, cycle, request, new LeadershipAssignment { Id = Guid.NewGuid(), OrganizationId = organizationId,
                OrganizationUserId = chief.Id, PositionKey = "chief-of-staff", StartsAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var report = new ManagementStatusReport(cycle.Id, "One blocker needs attention.", [], [], ["Deployment blocked"], [], [],
            ["Approve rollback"], [], 0.9m, DateTimeOffset.UtcNow)
        {
            RequestId = request.Id, Markdown = "# Chief of Staff briefing\n\n## Work on now\n- Resolve deployment blocker.",
            ImmediateActions = ["Resolve deployment blocker."], ConversationTopics = ["Approve rollback"], Severity = "Urgent"
        };
        var published = new PublishEvent { EventType = ManagementEvents.StatusReported, ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions(JsonSerializerDefaults.Web))) };
        var session = new AgentSession(Guid.NewGuid().ToString("N"), "chief", installationId.ToString("D"), organizationId.ToString("D"),
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AuthorizedAgentGrant(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>()));
        var observer = new ManagementEventObserver(db, new TestAuditEventWriter());

        await observer.ObserveAsync(session, published, CancellationToken.None);
        await observer.ObserveAsync(session, published, CancellationToken.None);

        Assert.Equal("Delivered", request.Status);
        Assert.Single(await db.CoreConversationMessages.ToListAsync());
        Assert.Single(await db.UserNotifications.ToListAsync());
        Assert.Single(await db.ExecutiveBriefingDeliveries.ToListAsync());
        Assert.Contains("## Work on now", (await db.CoreConversationMessages.SingleAsync()).Content);
    }

    [Fact]
    public async Task ExecutiveReport_RejectsUnsafeMarkdown()
    {
        await using var db = CreateDb();
        var organizationId = Guid.NewGuid(); var installationId = Guid.NewGuid(); var chiefId = Guid.NewGuid(); var requestId = Guid.NewGuid();
        db.CoreOrganizationUsers.Add(new OrganizationUser { Id = chiefId, OrganizationId = organizationId, DisplayName = "Chief",
            EmployeeType = EmployeeType.Agent, AgentInstallationId = installationId, CreatedAt = DateTimeOffset.UtcNow });
        db.ManagementCheckInRequests.Add(new ManagementCheckInRequestRecord { Id = requestId, OrganizationId = organizationId,
            ManagementCycleId = Guid.NewGuid(), RequestedByOrganizationUserId = Guid.NewGuid(), RequestedFromOrganizationUserId = chiefId,
            CheckInType = "ExecutiveBriefing", Status = "AwaitingReport", CreatedAt = DateTimeOffset.UtcNow, DueAt = DateTimeOffset.UtcNow.AddHours(1) });
        db.LeadershipAssignments.Add(new LeadershipAssignment { Id = Guid.NewGuid(), OrganizationId = organizationId,
            OrganizationUserId = chiefId, PositionKey = "chief-of-staff", StartsAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var report = new ManagementStatusReport(Guid.NewGuid(), "Unsafe", [], [], [], [], [], [], [], 0.5m, DateTimeOffset.UtcNow)
            { RequestId = requestId, Markdown = "<script>alert(1)</script>" };
        var published = new PublishEvent { EventType = ManagementEvents.StatusReported,
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(report, new JsonSerializerOptions(JsonSerializerDefaults.Web))) };
        var session = new AgentSession(Guid.NewGuid().ToString("N"), "chief", installationId.ToString("D"), organizationId.ToString("D"),
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AuthorizedAgentGrant(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>()));

        await new ManagementEventObserver(db, new TestAuditEventWriter()).ObserveAsync(session, published, CancellationToken.None);

        Assert.Equal("Failed", (await db.ManagementCheckInRequests.SingleAsync()).Status);
        Assert.Empty(await db.CoreConversationMessages.ToListAsync());
    }

    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
