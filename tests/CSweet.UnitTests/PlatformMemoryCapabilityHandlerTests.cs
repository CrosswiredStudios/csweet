using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.AgentHost.Broker;
using CSweet.Memory;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class PlatformMemoryCapabilityHandlerTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"csweet-platform-memory-{Guid.NewGuid():N}.db");
    private SqliteMemoryStore _store = null!;
    private PlatformMemoryCapabilityHandler _handler = null!;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        _store = new SqliteMemoryStore(_path);
        await _store.InitializeAsync();
        _handler = new PlatformMemoryCapabilityHandler(_store, NullLogger<PlatformMemoryCapabilityHandler>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
            if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
    }

    [Fact]
    public async Task UserMemory_RoundTripsThroughBrokerCapability()
    {
        var session = Session("business-a", "capability.request", "memory.user.propose", "memory.user.read");
        var partition = new MemoryPartition("business-a", "app", "agent", "user-1", "conversation-1");
        var now = DateTimeOffset.UtcNow;
        var episode = new MemoryEpisode(
            Guid.NewGuid(), partition, MemoryScope.User, "The customer prefers email updates.", "text/plain",
            new MemorySource("user", "message-1"), "checksum", now, now, Sensitivity: MemorySensitivity.Personal);

        var write = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Write, "append-episode", episode), CancellationToken.None);
        var search = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Query, "search",
            new MemorySearchRequest(partition, MemoryScope.User, "email updates")), CancellationToken.None);

        Assert.True(write.Succeeded, write.Error);
        Assert.True(search.Succeeded, search.Error);
        var candidates = JsonSerializer.Deserialize<IReadOnlyList<MemoryCandidate>>(search.Payload.Span, JsonOptions);
        Assert.Contains(candidates!, candidate => candidate.Id == episode.Id);
    }

    [Fact]
    public async Task CrossBusinessMemory_IsRejectedBeforeStoreAccess()
    {
        var session = Session("business-a", "capability.request", "memory.user.read");
        var partition = new MemoryPartition("business-b", UserId: "user-1");

        var result = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Query, "search",
            new MemorySearchRequest(partition, MemoryScope.User, "anything")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Cross-business", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CrossEmployeeMemory_IsRejectedForResolvedInstallationIdentity()
    {
        var session = Session("legacy-business", "capability.request", "memory.user.read");
        session.MemoryTenantId = "business-a";
        session.MemoryEmployeeId = "employee-a";
        var partition = new MemoryPartition("business-a", AgentId: "employee-b", UserId: "user-1");

        var result = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Query, "search",
            new MemorySearchRequest(partition, MemoryScope.User, "anything")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("Cross-employee", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BusinessNamespace_RequiresBusinessReadPermission()
    {
        var session = Session("business-a", "capability.request", "memory.user.read");
        var partition = EmployeeMemoryNamespaces.Organization("business-a", "app").Partition;

        var result = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Query, "search",
            new MemorySearchRequest(partition, MemoryScope.Tenant, "objective")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("memory.business.read", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteScope_RequiresManagePermission()
    {
        var session = Session("business-a", "capability.request", "memory.user.propose", "memory.user.read");
        var partition = new MemoryPartition("business-a", UserId: "user-1");

        var result = await _handler.HandleAsync(session, Request(
            CSweetMemoryCapabilities.Manage, "delete-scope", partition), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("memory.user.manage", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NonJsonRequest_IsRejected()
    {
        var session = Session("business-a", "capability.request", "memory.user.read");
        var request = Request(CSweetMemoryCapabilities.Query, "search", new { });
        request.ContentType = "text/plain";

        var result = await _handler.HandleAsync(session, request, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("application/json", result.Error, StringComparison.Ordinal);
    }

    private static AgentSession Session(string businessId, params string[] permissions) => new(
        Guid.NewGuid().ToString("N"), "test-agent", Guid.NewGuid().ToString("D"), businessId,
        Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
        new AuthorizedAgentGrant(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), permissions.ToHashSet(StringComparer.Ordinal)));

    private static RequestCapability Request(string capability, string operation, object payload)
    {
        var command = new CSweetMemoryCommand(operation, JsonSerializer.SerializeToElement(payload, JsonOptions));
        return new RequestCapability
        {
            RequestId = Guid.NewGuid().ToString("N"), Capability = capability, ContentType = "application/json",
            Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(command, JsonOptions))
        };
    }
}
