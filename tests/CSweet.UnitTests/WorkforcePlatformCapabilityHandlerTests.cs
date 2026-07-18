using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AgentHost.Broker;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class WorkforcePlatformCapabilityHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ReadProfile_RequiresAnExplicitInstallationGrant()
    {
        await using var db = CreateDb();
        var organization = Organization();
        db.CoreOrganizations.Add(organization);
        await db.SaveChangesAsync();
        var handler = new WorkforcePlatformCapabilityHandler(db, new TestAuditEventWriter(), Array.Empty<IWorkforceCatalogProvider>(), Array.Empty<IBusinessPatternProvider>());

        var result = await InvokeAsync(handler, Session(organization.Id, new HashSet<string>()), Request(PlatformCapabilities.BusinessProfileRead, new { }));

        Assert.False(result.Succeeded);
        var error = JsonSerializer.Deserialize<PlatformCapabilityError>(result.Payload.Span, JsonOptions);
        Assert.Equal(PlatformCapabilityErrorCode.Denied, error?.Code);
    }

    [Fact]
    public async Task WorkforceSearch_RemainsUsefulOfflineAndDoesNotInventMarketplaceCandidates()
    {
        await using var db = CreateDb();
        var organization = Organization();
        db.CoreOrganizations.Add(organization);
        db.CoreWorkers.Add(new Worker
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id, Name = "Local Researcher", Description = "Local",
            WorkerType = WorkerType.LocalAgent, ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[\"research.market-analysis\"]", IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var handler = new WorkforcePlatformCapabilityHandler(db, new TestAuditEventWriter(), Array.Empty<IWorkforceCatalogProvider>(), Array.Empty<IBusinessPatternProvider>());
        var request = new WorkforceSearchRequest(["research.market-analysis"], null, null, null, "USD", false, null);

        var result = await InvokeAsync(handler,
            Session(organization.Id, new HashSet<string> { PlatformCapabilities.WorkforceSearch }),
            Request(PlatformCapabilities.WorkforceSearch, request));
        var response = JsonSerializer.Deserialize<WorkforceSearchResponse>(result.Payload.Span, JsonOptions);

        Assert.True(result.Succeeded);
        Assert.NotNull(response);
        Assert.False(response.MarketplaceAvailable);
        Assert.Contains(response.Candidates, x => x.Name == "Local Researcher");
        Assert.Contains("No marketplace provider", response.UnavailableReason);
    }

    [Fact]
    public async Task WorkforceSearch_UsesDigitalMarketplaceBeforeHumanFallback()
    {
        await using var db = CreateDb();
        var organization = Organization();
        db.CoreOrganizations.Add(organization);
        await db.SaveChangesAsync();
        var digital = new FakeCatalog("digital", WorkforceCatalogKind.DigitalMarketplace,
            new WorkforceSearchResponse([
                new CSweet.Agent.SDK.WorkforceCandidate("digital-1", "Marketplace", "Agent", "Digital PM", ["delivery.management"], [], 50, "USD", 0.8m, "Match", true)
            ], [], true, null));
        var human = new FakeCatalog("human", WorkforceCatalogKind.HumanMarketplace,
            new WorkforceSearchResponse([], [], true, null));
        var handler = new WorkforcePlatformCapabilityHandler(db, new TestAuditEventWriter(), [digital, human], Array.Empty<IBusinessPatternProvider>());

        var result = await InvokeAsync(handler,
            Session(organization.Id, new HashSet<string> { PlatformCapabilities.WorkforceSearch }),
            Request(PlatformCapabilities.WorkforceSearch,
                new WorkforceSearchRequest(["delivery.management"], null, null, 100, "USD", false, null)));
        var response = JsonSerializer.Deserialize<WorkforceSearchResponse>(result.Payload.Span, JsonOptions);

        Assert.True(result.Succeeded);
        Assert.Contains(response!.Candidates, x => x.CandidateId == "digital-1");
        Assert.Equal(1, digital.SearchCount);
        Assert.Equal(0, human.SearchCount);
    }

    private static async Task<CapabilityResult> InvokeAsync(WorkforcePlatformCapabilityHandler handler, AgentSession session, RequestCapability request)
    {
        await foreach (var result in handler.HandleAsync(session, request, CancellationToken.None)) return result;
        throw new InvalidOperationException("Handler returned no result.");
    }

    private static AgentSession Session(Guid organizationId, IReadOnlySet<string> grants) => new(
        Guid.NewGuid().ToString("N"), "test-agent", Guid.NewGuid().ToString("D"), organizationId.ToString("D"),
        Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
        new AuthorizedAgentGrant(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), grants));

    private static RequestCapability Request<T>(string capability, T payload) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"), Capability = capability, ContentType = "application/json",
        Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
    };

    private static Organization Organization() => new()
    {
        Id = Guid.NewGuid(), Name = "Example", Status = OrganizationStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
    };

    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeCatalog(string key, WorkforceCatalogKind kind, WorkforceSearchResponse response) : IWorkforceCatalogProvider
    {
        public string ProviderKey => key;
        public WorkforceCatalogKind CatalogKind => kind;
        public int SearchCount { get; private set; }
        public Task<WorkforceSearchResponse> SearchAsync(WorkforceSearchRequest request, CancellationToken cancellationToken = default)
        {
            SearchCount++;
            return Task.FromResult(response);
        }
    }
}
