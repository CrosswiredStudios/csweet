using System.Text.Json;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class AgentInstallationServiceTests
{
    [Fact]
    public async Task InstallAsync_RejectsGrantBroaderThanManifest()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = new AgentInstallationService(dbContext, new TestAuditEventWriter());
        var request = ValidRequest() with
        {
            GrantedCapabilities = ["research.execute.v1", "admin.delete.v1"]
        };

        var exception = await Assert.ThrowsAsync<AgentInstallationException>(() =>
            service.InstallAsync(package.Id, request));

        Assert.Contains("manifest did not request", exception.Message);
        Assert.Empty(await dbContext.AgentInstallations.ToListAsync());
    }

    [Fact]
    public async Task InstallAsync_RejectsTickFrequencyBelowGlobalMinimum()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = new AgentInstallationService(dbContext, new TestAuditEventWriter());

        var exception = await Assert.ThrowsAsync<AgentInstallationException>(() =>
            service.InstallAsync(package.Id, ValidRequest() with { TickFrequencySeconds = 299 }));

        Assert.Contains("at least 300 seconds", exception.Message);
        Assert.Empty(await dbContext.AgentInstallations.ToListAsync());
    }

    [Fact]
    public async Task InstallAsync_RejectsRevokedPackageVersion()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        package.Status = AgentPackageVersionStatus.Revoked;
        await dbContext.SaveChangesAsync();
        var service = new AgentInstallationService(dbContext, new TestAuditEventWriter());

        var exception = await Assert.ThrowsAsync<AgentInstallationException>(() =>
            service.InstallAsync(package.Id, ValidRequest()));

        Assert.Contains("not available for installation", exception.Message);
        Assert.Empty(await dbContext.AgentInstallations.ToListAsync());
    }

    [Fact]
    public async Task InstallAsync_CreatesInstallationGrantAndPeriodicSchedule()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = new AgentInstallationService(dbContext, new TestAuditEventWriter());
        var before = DateTimeOffset.UtcNow.AddSeconds(899);

        var result = await service.InstallAsync(package.Id, ValidRequest());

        Assert.True(result.IsEnabled);
        Assert.Equal("Periodic", result.Schedule.ActivationMode);
        Assert.True(result.Schedule.NextTickAt >= before);
        Assert.Single(await dbContext.AgentInstallations.ToListAsync());
        Assert.Single(await dbContext.AgentInstallationGrants.ToListAsync());
        Assert.Single(await dbContext.AgentSchedules.ToListAsync());
        Assert.Equal(
            AgentPackageVersionStatus.Approved,
            (await dbContext.AgentPackageVersions.SingleAsync()).Status);
    }

    private static InstallAgentRequest ValidRequest() => new(
        "default",
        "Periodic",
        900,
        "Skip",
        ["research.execute.v1"],
        ["research.requested.v1"],
        ["research.completed.v1"],
        [],
        [],
        600,
        512,
        50);

    private static async Task<AgentPackageVersion> SeedAsync(CSweetDbContext dbContext)
    {
        dbContext.AgentRuntimeGlobalSettings.Add(new AgentRuntimeGlobalSettings
        {
            Id = Guid.NewGuid(),
            EnableImportedAgents = true,
            DefaultActivationMode = ActivationMode.Periodic,
            DefaultOverlapPolicy = OverlapPolicy.Skip,
            DefaultRestartPolicy = RestartPolicy.Never,
            MinimumTickFrequencySeconds = 300,
            DefaultMaxRuntimeSeconds = 600,
            MaximumContainerMemoryMb = 2048,
            MaximumContainerCpuPercent = 200,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        var package = new AgentPackageVersion
        {
            Id = Guid.NewGuid(),
            PackageSourceId = Guid.NewGuid(),
            CommitSha = "0123456789abcdef0123456789abcdef01234567",
            ManifestDigest = new string('a', 64),
            ManifestJson = JsonSerializer.Serialize(new
            {
                manifestVersion = "1.0",
                id = "com.example.research-agent",
                name = "Research Agent",
                version = "1.2.3",
                publisher = new { id = "com.example", name = "Example" },
                runtime = new { type = "dotnet-project" },
                protocol = new { minimumVersion = "1.0", maximumVersion = "1.x" },
                capabilities = new[] { "research.execute.v1" },
                requestedSubscriptions = new[] { "research.requested.v1" },
                requestedPublications = new[] { "research.completed.v1" }
            }),
            AgentId = "com.example.research-agent",
            AgentName = "Research Agent",
            Version = "1.2.3",
            PublisherId = "com.example",
            PublisherName = "Example",
            RuntimeType = "dotnet-project",
            Status = AgentPackageVersionStatus.Previewed,
            ImportedAt = DateTimeOffset.UtcNow
        };
        dbContext.AgentPackageVersions.Add(package);
        await dbContext.SaveChangesAsync();
        return package;
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CSweetDbContext(options);
    }
}