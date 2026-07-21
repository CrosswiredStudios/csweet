using System.Text.Json;
using CSweet.Agent.Contracts.Grpc;
using CSweet.Agent.SDK;
using CSweet.AgentHost.Broker;
using CSweet.Contracts.Communications;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Communications;
using CSweet.Infrastructure.Core;
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

    [Fact]
    public async Task AskUser_IsGlobalPersistsDecisionAndReturnsRecommendedOptionFirst()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(), Name = "Example", Status = OrganizationStatus.Active,
            CreatedAt = now, UpdatedAt = now
        };
        var installationId = Guid.NewGuid();
        var chief = new OrganizationUser
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id, AgentInstallationId = installationId,
            DisplayName = "Chief of Staff", EmployeeType = EmployeeType.Agent,
            PermissionLevel = OrganizationPermissionLevel.Manager, IsActive = true, CreatedAt = now
        };
        var owner = new OrganizationUser
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id, DisplayName = "Owner",
            EmployeeType = EmployeeType.Human, PermissionLevel = OrganizationPermissionLevel.Owner,
            IsActive = true, CreatedAt = now
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), OrganizationId = organization.Id,
            AgentOrganizationUserId = chief.Id, InitiatedByOrganizationUserId = owner.Id,
            Kind = ConversationKind.DirectHumanAgent, CreatedAt = now, UpdatedAt = now
        };
        var turnId = Guid.NewGuid();
        var userMessage = new ConversationMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, ChatTurnId = turnId,
            Role = ConversationRole.User, Content = "Help me structure the team.",
            CreatedAt = now, CorrelationId = Guid.NewGuid()
        };
        db.AddRange(
            organization,
            chief,
            owner,
            conversation,
            new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = conversation.Id, OrganizationUserId = chief.Id, JoinedAt = now },
            new ConversationParticipant { Id = Guid.NewGuid(), ConversationId = conversation.Id, OrganizationUserId = owner.Id, JoinedAt = now },
            userMessage,
            new ChatTurn
            {
                Id = turnId, OrganizationId = organization.Id, ConversationId = conversation.Id,
                TargetAgentOrganizationUserId = chief.Id, UserMessageId = userMessage.Id,
                Status = ChatTurnStatus.Running, CreatedAt = now, UpdatedAt = now
            });
        await db.SaveChangesAsync();
        var turns = new ChatTurnService(db);
        var decisions = new ExecutiveDecisionService(db, turns);
        var hub = new CommunicationHubService(db, new TestAuditEventWriter(), turns, decisions);
        var handler = new CommunicationHubCapabilityHandler(db, hub, decisions);
        var payload = new
        {
            conversationId = conversation.Id,
            chatTurnId = turnId,
            prompt = "How should we structure the initial development team?",
            options = new[]
            {
                new { id = "internal", label = "Internal team", description = "Hire directly." },
                new { id = "agency", label = "Agency", description = "Contract a vetted agency." },
                new { id = "low-code", label = "Low-code", description = "Prototype first." }
            },
            recommendedOptionId = "agency",
            idempotencyKey = $"team-structure:{turnId:N}"
        };

        var result = await InvokeAsync(
            handler,
            Session(organization.Id, installationId, new HashSet<string>()),
            Request(CommunicationHubCapabilities.AskUser, payload));

        Assert.True(result.Succeeded, result.Error);
        var card = JsonSerializer.Deserialize<ExecutiveDecisionCardResponse>(result.Payload.Span, JsonOptions)!;
        Assert.Equal("agency", card.RecommendedOptionId);
        Assert.Equal("agency", card.Options[0].Id);
        Assert.True(card.Options[0].Recommended);
        Assert.Single(await db.ExecutiveDecisions.ToListAsync());
        db.CoreConversationMessages.Add(new ConversationMessage
        {
            Id = Guid.NewGuid(), ConversationId = conversation.Id, ChatTurnId = turnId,
            SenderOrganizationUserId = chief.Id, Role = ConversationRole.Assistant,
            Content = "I've prepared the team-structure decision.", CreatedAt = now.AddSeconds(1),
            CorrelationId = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        var messages = await hub.ListMessagesAsync(organization.Id, conversation.Id, owner.Id);
        Assert.Contains(messages!, message => message.Decision?.Id == card.Id);
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
