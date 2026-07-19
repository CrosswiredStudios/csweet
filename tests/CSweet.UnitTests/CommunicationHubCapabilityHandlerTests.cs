using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AgentHost.Broker;
using CSweet.Contracts.Communications;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Communications;
using CSweet.Infrastructure.Persistence;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class CommunicationHubCapabilityHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateChat_RequiresExplicitGrantThenCreatesAsAgentEmployee()
    {
        await using var db = CreateDb();
        var organization = new Organization { Id = Guid.NewGuid(), Name = "Example", Status = OrganizationStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var installationId = Guid.NewGuid();
        var chief = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organization.Id, AgentInstallationId = installationId,
            DisplayName = "Chief of Staff", EmployeeType = EmployeeType.Agent, PermissionLevel = OrganizationPermissionLevel.Manager,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow };
        var member = new OrganizationUser { Id = Guid.NewGuid(), OrganizationId = organization.Id, DisplayName = "Product Lead",
            EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Contributor, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow };
        db.AddRange(organization, chief, member);
        await db.SaveChangesAsync();
        var service = new CommunicationHubService(
            db,
            new TestAuditEventWriter(),
            new CSweet.Infrastructure.Core.ChatTurnService(db));
        var handler = new CommunicationHubCapabilityHandler(db, service);
        var payload = new CreateCommunicationChatRequest("product", null, false, false, [member.Id]);

        var denied = await InvokeAsync(handler, Session(organization.Id, installationId, new HashSet<string>()), Request(CommunicationHubCapabilities.Create, payload));
        var allowed = await InvokeAsync(handler, Session(organization.Id, installationId,
            new HashSet<string> { CommunicationHubCapabilities.Create }), Request(CommunicationHubCapabilities.Create, payload));

        Assert.False(denied.Succeeded);
        Assert.True(allowed.Succeeded, allowed.Error);
        var action = JsonSerializer.Deserialize<CommunicationHubActionResponse>(allowed.Payload.Span, JsonOptions);
        Assert.True(action?.Succeeded);
        Assert.Equal(chief.Id, (await db.CoreConversations.Include(x => x.Participants).SingleAsync()).InitiatedByOrganizationUserId);
    }

    private static async Task<CapabilityResult> InvokeAsync(CommunicationHubCapabilityHandler handler, AgentSession session, RequestCapability request)
    {
        await foreach (var result in handler.HandleAsync(session, request, CancellationToken.None)) return result;
        throw new InvalidOperationException("Handler returned no result.");
    }
    private static AgentSession Session(Guid organizationId, Guid installationId, IReadOnlySet<string> grants) => new(
        Guid.NewGuid().ToString("N"), "chief", installationId.ToString("D"), organizationId.ToString("D"),
        Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
        new AuthorizedAgentGrant(new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), grants));
    private static RequestCapability Request<T>(string capability, T payload) => new()
    {
        RequestId = Guid.NewGuid().ToString("N"), Capability = capability, ContentType = "application/json",
        Payload = ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions))
    };
    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
