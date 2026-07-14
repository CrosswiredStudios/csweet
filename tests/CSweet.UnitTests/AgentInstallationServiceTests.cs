using System.Text.Json;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class AgentInstallationServiceTests
{
    [Fact]
    public async Task InstallAsync_RejectsGrantBroaderThanManifest()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
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
        var service = CreateService(dbContext);

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
        var service = CreateService(dbContext);

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
        var service = CreateService(dbContext);
        var before = DateTimeOffset.UtcNow.AddSeconds(899);

        var result = await service.InstallAsync(package.Id, ValidRequest());

        Assert.True(result.IsEnabled);
        Assert.Equal("Periodic", result.Schedule.ActivationMode);
        Assert.True(result.Schedule.NextTickAt >= before);
        Assert.Single(await dbContext.AgentInstallations.ToListAsync());
        Assert.Single(await dbContext.AgentInstallationGrants.ToListAsync());
        Assert.Single(await dbContext.AgentSchedules.ToListAsync());
        var buildJob = Assert.Single(await dbContext.AgentBuildJobs.ToListAsync());
        Assert.Equal(AgentBuildStatus.Queued, buildJob.Status);
        Assert.Equal(
            AgentPackageVersionStatus.Approved,
            (await dbContext.AgentPackageVersions.SingleAsync()).Status);
    }

    [Fact]
    public async Task InstallAsync_ReusesPackageBuildAcrossBusinessInstallations()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = CreateService(dbContext);

        await service.InstallAsync(package.Id, ValidRequest());
        await service.InstallAsync(
            package.Id,
            ValidRequest() with { BusinessId = "second-business" });

        Assert.Equal(2, await dbContext.AgentInstallations.CountAsync());
        Assert.Single(await dbContext.AgentBuildJobs.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_LastInstallation_RemovesPackageSourceAndRelatedRecords()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        var installation = await service.InstallAsync(package.Id, ValidRequest());

        var result = await service.RemoveAsync(installation.Id);

        Assert.True(result.PackageRemoved);
        Assert.True(result.SourceRemoved);
        Assert.Equal(0, result.CleanupWarnings);
        Assert.Empty(await dbContext.AgentInstallations.ToListAsync());
        Assert.Empty(await dbContext.AgentInstallationGrants.ToListAsync());
        Assert.Empty(await dbContext.AgentSchedules.ToListAsync());
        Assert.Empty(await dbContext.AgentBuildJobs.ToListAsync());
        Assert.Empty(await dbContext.AgentPackageVersions.ToListAsync());
        Assert.Empty(await dbContext.AgentPackageSources.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_SharedPackage_PreservesPackageAndOtherInstallation()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        var first = await service.InstallAsync(package.Id, ValidRequest());
        await service.InstallAsync(package.Id, ValidRequest() with { BusinessId = "second-business" });

        var result = await service.RemoveAsync(first.Id);

        Assert.False(result.PackageRemoved);
        Assert.False(result.SourceRemoved);
        Assert.Single(await dbContext.AgentInstallations.ToListAsync());
        Assert.Single(await dbContext.AgentPackageVersions.ToListAsync());
        Assert.Single(await dbContext.AgentPackageSources.ToListAsync());
        Assert.Single(await dbContext.AgentBuildJobs.ToListAsync());
    }

    [Fact]
    public async Task RemoveAsync_RemovesRetainedRuntimeContainer()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var containers = new TestAgentContainerRunner(containerExists: true);
        var service = CreateService(dbContext, containers);
        var installation = await service.InstallAsync(package.Id, ValidRequest());
        dbContext.AgentRuntimeInstances.Add(new AgentRuntimeInstance
        {
            Id = Guid.NewGuid(),
            TickId = Guid.NewGuid(),
            AgentInstallationId = installation.Id,
            ContainerId = "retained-container",
            QueuedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        await service.RemoveAsync(installation.Id);

        Assert.Contains("retained-container", containers.Removed);
    }

    [Fact]
    public async Task RemoveAsync_RejectsRemovalWhilePackageIsBuilding()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var service = CreateService(dbContext);
        var installation = await service.InstallAsync(package.Id, ValidRequest());
        var build = await dbContext.AgentBuildJobs.SingleAsync();
        build.TransitionTo(AgentBuildStatus.Cloning, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<AgentInstallationException>(
            () => service.RemoveAsync(installation.Id));

        Assert.Contains("currently building", exception.Message);
        Assert.Single(await dbContext.AgentInstallations.ToListAsync());
    }

    [Fact]
    public async Task ListRunsAsync_IncludesLiveContainerOutput()
    {
        await using var dbContext = CreateDbContext();
        var package = await SeedAsync(dbContext);
        var containers = new TestAgentContainerRunner(containerExists: true, logs: "agent connected to broker");
        var service = CreateService(dbContext, containers);
        var installation = await service.InstallAsync(package.Id, ValidRequest());
        dbContext.AgentRuntimeInstances.Add(new AgentRuntimeInstance
        {
            Id = Guid.NewGuid(),
            TickId = Guid.NewGuid(),
            AgentInstallationId = installation.Id,
            ContainerId = "running-container",
            QueuedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var run = Assert.Single(await service.ListRunsAsync(installation.Id));

        Assert.Equal("agent connected to broker", run.LogExcerpt);
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
        var source = new AgentPackageSource
        {
            Id = Guid.NewGuid(),
            RepositoryUrl = "https://github.com/example/research-agent",
            Host = "github.com",
            RepositoryOwner = "example",
            RepositoryName = "research-agent",
            DefaultBranch = "main",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        var package = new AgentPackageVersion
        {
            Id = Guid.NewGuid(),
            PackageSourceId = source.Id,
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
        dbContext.AgentPackageSources.Add(source);
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

    private static AgentInstallationService CreateService(
        CSweetDbContext dbContext,
        TestAgentContainerRunner? containers = null) =>
        new(
            dbContext,
            new TestAuditEventWriter(),
            containers ?? new TestAgentContainerRunner(),
            NullLogger<AgentInstallationService>.Instance);

    private sealed class TestAgentContainerRunner(bool containerExists = false, string logs = "") : IAgentContainerRunner
    {
        public List<string> Removed { get; } = [];

        public Task<AgentContainerStatus> StartAsync(AgentContainerStartRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task StopAsync(string containerId, TimeSpan gracePeriod, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AgentContainerStatus?> InspectAsync(string containerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AgentContainerStatus?>(containerExists
                ? new AgentContainerStatus(containerId, containerId, AgentContainerState.Exited, 0, null, null, null)
                : null);

        public Task RemoveAsync(string containerId, bool force = false, CancellationToken cancellationToken = default)
        {
            Removed.Add(containerId);
            return Task.CompletedTask;
        }

        public Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default) =>
            Task.FromResult(logs);
    }
}
