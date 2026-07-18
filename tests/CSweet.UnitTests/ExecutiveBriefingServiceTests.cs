using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class ExecutiveBriefingServiceTests
{
    [Fact]
    public async Task ManualRequest_IsDurableAndIdempotentRuntimeStartupUsesCooldown()
    {
        await using var db = CreateDb();
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Example", Status = OrganizationStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var owner = Person(organization.Id, "Owner", EmployeeType.Human, OrganizationPermissionLevel.Owner);
        var installationId = Guid.NewGuid();
        var chief = Person(organization.Id, "Chief", EmployeeType.Agent, OrganizationPermissionLevel.Manager);
        chief.AgentInstallationId = installationId; chief.ReportsToOrganizationUserId = owner.Id;
        var cycle = new ManagementCycle { Id = Guid.NewGuid(), OrganizationId = organization.Id, TimeZone = "UTC" };
        db.AddRange(organization, owner, chief, cycle, new LeadershipAssignment
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id, OrganizationUserId = chief.Id,
            PositionKey = "chief-of-staff", StartsAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new ExecutiveBriefingService(db, new TestAuditEventWriter(), TimeProvider.System);

        var manual = await service.QueueManualAsync(organization.Id);
        var firstStartup = await service.QueueRuntimeStartupAsync(installationId, Guid.NewGuid());
        var secondStartup = await service.QueueRuntimeStartupAsync(installationId, Guid.NewGuid());

        Assert.True(manual.Succeeded);
        Assert.True(firstStartup.Succeeded);
        Assert.Equal(firstStartup.RequestId, secondStartup.RequestId);
        Assert.Equal(2, await db.ManagementCheckInRequests.CountAsync());
        Assert.All(await db.ManagementCheckInRequests.ToListAsync(), x => Assert.Equal("ExecutiveBriefing", x.CheckInType));
    }

    [Fact]
    public async Task Settings_RejectReportingCycleAndAcceptConfiguredManager()
    {
        await using var db = CreateDb();
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Example", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var owner = Person(organization.Id, "Owner", EmployeeType.Human, OrganizationPermissionLevel.Owner);
        var chief = Person(organization.Id, "Chief", EmployeeType.Agent, OrganizationPermissionLevel.Manager);
        chief.ReportsToOrganizationUserId = owner.Id;
        var subordinate = Person(organization.Id, "Subordinate", EmployeeType.Human, OrganizationPermissionLevel.Contributor);
        subordinate.ReportsToOrganizationUserId = chief.Id;
        db.AddRange(organization, owner, chief, subordinate, new ManagementCycle { Id = Guid.NewGuid(), OrganizationId = organization.Id },
            new LeadershipAssignment { Id = Guid.NewGuid(), OrganizationId = organization.Id, OrganizationUserId = chief.Id,
                PositionKey = "chief-of-staff", StartsAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var service = new ExecutiveBriefingService(db, new TestAuditEventWriter(), TimeProvider.System);

        var invalid = await service.UpdateSettingsAsync(organization.Id,
            new UpdateExecutiveBriefingSettingsRequest(subordinate.Id, true, true, "Weekdays", "Friday", "09:00", "UTC"));
        var valid = await service.UpdateSettingsAsync(organization.Id,
            new UpdateExecutiveBriefingSettingsRequest(owner.Id, true, true, "Weekly", "Monday", "08:30", "UTC"));

        Assert.False(invalid.Succeeded);
        Assert.Equal("invalid_hierarchy", invalid.ErrorCode);
        Assert.True(valid.Succeeded);
        Assert.Equal("Weekly", (await db.ManagementCycles.SingleAsync()).ExecutiveBriefingCadence);
    }

    [Fact]
    public void ScheduleCalculator_UsesWeekdaysAndQuietHours()
    {
        var cycle = new ManagementCycle { TimeZone = "UTC", ExecutiveBriefingCadence = "Weekdays",
            ExecutiveBriefingLocalTime = "09:00", QuietHoursStart = "18:00", QuietHoursEnd = "08:00" };
        var fridayAfter = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

        var next = ExecutiveBriefingScheduleCalculator.Next(fridayAfter, cycle);

        Assert.Equal(DayOfWeek.Monday, next.DayOfWeek);
        Assert.Equal(9, next.Hour);
        Assert.True(ExecutiveBriefingScheduleCalculator.IsQuietHours(
            new DateTimeOffset(2026, 7, 17, 20, 0, 0, TimeSpan.Zero), cycle));
    }

    [Fact]
    public void ScheduleCalculator_NormalizesInvalidDaylightSavingTime()
    {
        var zoneId = OperatingSystem.IsWindows() ? "Pacific Standard Time" : "America/Los_Angeles";
        var cycle = new ManagementCycle { TimeZone = zoneId, ExecutiveBriefingCadence = "Daily", ExecutiveBriefingLocalTime = "02:30" };
        var beforeTransition = new DateTimeOffset(2026, 3, 8, 8, 0, 0, TimeSpan.Zero);

        var next = ExecutiveBriefingScheduleCalculator.Next(beforeTransition, cycle);
        var local = TimeZoneInfo.ConvertTime(next, TimeZoneInfo.FindSystemTimeZoneById(zoneId));

        Assert.Equal(new TimeSpan(3, 30, 0), local.TimeOfDay);
    }

    private static OrganizationUser Person(Guid organizationId, string name, EmployeeType type, OrganizationPermissionLevel permission) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, DisplayName = name, EmployeeType = type,
        PermissionLevel = permission, CreatedAt = DateTimeOffset.UtcNow, IsActive = true
    };

    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
