using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CSweet.UnitTests;

public sealed class AgentRuntimeManagerTests
{
    [Fact]
    public async Task DuePeriodicSchedule_StartsOneRuntimeAndSchedulesNextTick()
    {
        await using var db = CreateDb();
        var installation = await SeedAsync(db);
        var containers = new FakeRunner();
        var manager = CreateManager(db, containers);

        Assert.Equal(1, await manager.ProcessDueSchedulesAsync());
        Assert.Equal(1, await manager.ReconcileAsync());

        var runtime = await db.AgentRuntimeInstances.SingleAsync();
        Assert.Equal(AgentRuntimeStatus.WaitingForBrokerRegistration, runtime.Status);
        Assert.Single(containers.Starts);
        Assert.True(installation.Schedule!.NextTickAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SkipOverlap_RecordsSkippedTickWithoutStartingSecondContainer()
    {
        await using var db = CreateDb();
        var installation = await SeedAsync(db);
        db.AgentRuntimeInstances.Add(RunningInstance(installation.Id));
        await db.SaveChangesAsync();
        var containers = new FakeRunner();
        var manager = CreateManager(db, containers);

        await manager.ProcessDueSchedulesAsync();
        await manager.ReconcileAsync();

        Assert.Contains(await db.AgentRuntimeInstances.ToListAsync(), x => x.Status == AgentRuntimeStatus.Skipped);
        Assert.Empty(containers.Starts);
    }

    [Fact]
    public async Task ConcurrencyLimit_LeavesRuntimeQueued()
    {
        await using var db = CreateDb();
        var installation = await SeedAsync(db, globalLimit: 1);
        var other = await SeedAsync(db, businessId: "business-2", due: false);
        db.AgentRuntimeInstances.Add(RunningInstance(other.Id));
        await db.SaveChangesAsync();
        var containers = new FakeRunner();
        var manager = CreateManager(db, containers);

        await manager.ProcessDueSchedulesAsync();
        await manager.ReconcileAsync();

        Assert.Contains(await db.AgentRuntimeInstances.ToListAsync(), x => x.AgentInstallationId == installation.Id && x.Status == AgentRuntimeStatus.Queued);
        Assert.Empty(containers.Starts);
    }

    [Fact]
    public async Task CompletionSignal_StopsContainerAndCompletesRuntime()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var containers = new FakeRunner();
        var manager = CreateManager(db, containers);
        await manager.ProcessDueSchedulesAsync();
        await manager.ReconcileAsync();
        var request = Assert.Single(containers.Starts);
        var signals = new AgentRuntimeSignalService(db);
        await signals.RecordBrokerRegistrationAsync(request.RuntimeInstanceId, request.TickId, request.InstallationId, request.WorkloadToken);
        await signals.RecordCompletionAsync(request.RuntimeInstanceId, request.TickId, request.InstallationId, "{\"succeeded\":true}");

        await manager.ReconcileAsync();

        Assert.Equal(AgentRuntimeStatus.Completed, (await db.AgentRuntimeInstances.SingleAsync()).Status);
        Assert.Single(containers.Stops);
        Assert.Single(containers.Removes);
    }

    [Fact]
    public async Task ExpiredRunningRuntime_IsStoppedAsTimedOut()
    {
        await using var db = CreateDb();
        var installation = await SeedAsync(db, due: false);
        var runtime = RunningInstance(installation.Id);
        runtime.ContainerId = "container-id";
        runtime.RuntimeDeadlineAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        db.AgentRuntimeInstances.Add(runtime);
        await db.SaveChangesAsync();
        var containers = new FakeRunner();

        await CreateManager(db, containers).ReconcileAsync();

        Assert.Equal(AgentRuntimeStatus.RuntimeTimedOut, runtime.Status);
        Assert.Single(containers.Stops);
    }

    private static AgentRuntimeManager CreateManager(CSweetDbContext db, IAgentContainerRunner runner) =>
        new(db, runner, Options.Create(new AgentRuntimeManagerOptions { BrokerEndpoint = "http://broker:8080", DockerNetworkName = "broker-only" }), NullLogger<AgentRuntimeManager>.Instance);

    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<AgentInstallation> SeedAsync(CSweetDbContext db, string businessId = "business-1", bool due = true, int globalLimit = 10)
    {
        if (!await db.AgentRuntimeGlobalSettings.AnyAsync())
            db.AgentRuntimeGlobalSettings.Add(new AgentRuntimeGlobalSettings { Id = Guid.NewGuid(), EnableImportedAgents = true, GlobalMaxActiveContainers = globalLimit, PerBusinessMaxActiveContainers = 5, PerInstallationMaxActiveContainers = 1, DefaultContainerPidsLimit = 100, BrokerRegistrationTimeoutSeconds = 30, ContainerStopGraceSeconds = 1, RemoveContainersAfterCompletion = true, DotNetRuntimeBaseImage = "runtime:9.0" });
        var package = new AgentPackageVersion { Id = Guid.NewGuid(), PackageSourceId = Guid.NewGuid(), CommitSha = new string('a', 40), ManifestDigest = new string('b', 64), ManifestJson = "{}", AgentId = "com.example.agent", AgentName = "Agent", Version = "1.0.0", PublisherId = "example", PublisherName = "Example", RuntimeType = "dotnet-project", ProjectPath = "src/Example.Agent.csproj", WarningsJson = "[]", Status = AgentPackageVersionStatus.Built, PackagePath = "C:\\packages\\agent", PackageDigest = new string('c', 64), ImportedAt = DateTimeOffset.UtcNow };
        var installation = new AgentInstallation { Id = Guid.NewGuid(), PackageVersionId = package.Id, BusinessId = businessId, IsEnabled = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, PackageVersion = package };
        installation.Grant = new AgentInstallationGrant { Id = Guid.NewGuid(), AgentInstallationId = installation.Id, MemoryMb = 512, CpuPercent = 50, MaxRuntimeSeconds = 60, ApprovedAt = DateTimeOffset.UtcNow };
        installation.Schedule = new AgentSchedule { Id = Guid.NewGuid(), AgentInstallationId = installation.Id, ActivationMode = ActivationMode.Periodic, TickFrequencySeconds = 300, NextTickAt = due ? DateTimeOffset.UtcNow.AddSeconds(-1) : DateTimeOffset.UtcNow.AddHours(1), MaxRuntimeSeconds = 60, OverlapPolicy = OverlapPolicy.Skip, IsEnabled = true };
        db.AgentInstallations.Add(installation);
        await db.SaveChangesAsync();
        return installation;
    }

    private static AgentRuntimeInstance RunningInstance(Guid installationId)
    {
        var instance = new AgentRuntimeInstance { Id = Guid.NewGuid(), TickId = Guid.NewGuid(), AgentInstallationId = installationId, QueuedAt = DateTimeOffset.UtcNow.AddMinutes(-1), WorkloadTokenHash = new string('0', 64), RuntimeDeadlineAt = DateTimeOffset.UtcNow.AddMinutes(5) };
        instance.TransitionTo(AgentRuntimeStatus.Starting, DateTimeOffset.UtcNow.AddMinutes(-1));
        instance.TransitionTo(AgentRuntimeStatus.WaitingForBrokerRegistration, DateTimeOffset.UtcNow.AddMinutes(-1));
        instance.TransitionTo(AgentRuntimeStatus.Running, DateTimeOffset.UtcNow.AddMinutes(-1));
        return instance;
    }

    private sealed class FakeRunner : IAgentContainerRunner
    {
        public List<AgentContainerStartRequest> Starts { get; } = [];
        public List<string> Stops { get; } = [];
        public List<string> Removes { get; } = [];
        public Task<AgentContainerStatus> StartAsync(AgentContainerStartRequest request, CancellationToken cancellationToken = default) { Starts.Add(request); return Task.FromResult(new AgentContainerStatus("container-id", request.ContainerName, AgentContainerState.Running, null, DateTimeOffset.UtcNow, null, null)); }
        public Task StopAsync(string containerId, TimeSpan gracePeriod, CancellationToken cancellationToken = default) { Stops.Add(containerId); return Task.CompletedTask; }
        public Task<AgentContainerStatus?> InspectAsync(string containerId, CancellationToken cancellationToken = default) => Task.FromResult<AgentContainerStatus?>(new AgentContainerStatus(containerId, "agent", AgentContainerState.Running, null, null, null, null));
        public Task RemoveAsync(string containerId, bool force = false, CancellationToken cancellationToken = default) { Removes.Add(containerId); return Task.CompletedTask; }
        public Task<string> GetLogsAsync(string containerId, int maximumBytes, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
    }
}
