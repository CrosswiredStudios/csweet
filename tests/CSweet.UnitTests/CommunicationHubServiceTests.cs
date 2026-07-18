using CSweet.Contracts.Communications;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Communications;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class CommunicationHubServiceTests
{
    [Fact]
    public async Task CreateGroup_ExpandsRoleAudienceAndPersistsMessages()
    {
        await using var db = CreateDb();
        var organization = Organization();
        var department = new Role { Id = Guid.NewGuid(), OrganizationId = organization.Id, Name = "Product",
            Description = "Product department", AuthorityLevel = AuthorityLevel.ExecutionWithApproval,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var manager = User(organization.Id, "Morgan", OrganizationPermissionLevel.Manager);
        var designer = User(organization.Id, "Drew", OrganizationPermissionLevel.Contributor, department.Id);
        var engineer = User(organization.Id, "Ellis", OrganizationPermissionLevel.Contributor, department.Id);
        db.AddRange(organization, department, manager, designer, engineer);
        await db.SaveChangesAsync();
        var service = new CommunicationHubService(db, new TestAuditEventWriter());

        var created = await service.CreateAsync(organization.Id, manager.Id,
            new CreateCommunicationChatRequest("product-launch", "Launch coordination", false, false,
                [], [department.Id], []));

        Assert.True(created.Succeeded);
        Assert.Equal(3, created.Chat!.Participants.Count);
        Assert.Contains(created.Chat.Participants, x => x.OrganizationUserId == designer.Id);
        var sent = await service.SendAsync(organization.Id, created.Chat.Id, designer.Id,
            new SendCommunicationMessageRequest("Design review is ready."));
        Assert.NotNull(sent);
        var messages = await service.ListMessagesAsync(organization.Id, created.Chat.Id, engineer.Id);
        Assert.NotNull(messages);
        Assert.Single(messages);
        Assert.Equal("Drew", messages[0].SenderDisplayName);
    }

    [Fact]
    public async Task GroupManagement_IsScopedAndArchivePreservesHistory()
    {
        await using var db = CreateDb();
        var organization = Organization();
        var manager = User(organization.Id, "Manager", OrganizationPermissionLevel.Manager);
        var member = User(organization.Id, "Member", OrganizationPermissionLevel.Contributor);
        var outsider = User(organization.Id, "Outsider", OrganizationPermissionLevel.Contributor);
        db.AddRange(organization, manager, member, outsider);
        await db.SaveChangesAsync();
        var service = new CommunicationHubService(db, new TestAuditEventWriter());
        var created = await service.CreateAsync(organization.Id, manager.Id,
            new CreateCommunicationChatRequest("operations", null, false, true, [member.Id]));
        await service.SendAsync(organization.Id, created.Chat!.Id, member.Id, new SendCommunicationMessageRequest("Status update"));

        Assert.Null(await service.ListMessagesAsync(organization.Id, created.Chat.Id, outsider.Id));
        var denied = await service.ArchiveAsync(organization.Id, created.Chat.Id, outsider.Id);
        Assert.False(denied.Succeeded);
        Assert.Equal("not_authorized", denied.ErrorCode);

        var archived = await service.ArchiveAsync(organization.Id, created.Chat.Id, manager.Id);
        Assert.True(archived.Succeeded);
        Assert.NotNull((await db.CoreConversations.SingleAsync()).ArchivedAt);
        Assert.Single(await db.CoreConversationMessages.ToListAsync());
    }

    [Fact]
    public async Task Contributor_CanStartDirectMessageButCannotCreateGroup()
    {
        await using var db = CreateDb();
        var organization = Organization();
        var first = User(organization.Id, "First", OrganizationPermissionLevel.Contributor);
        var second = User(organization.Id, "Second", OrganizationPermissionLevel.Contributor);
        db.AddRange(organization, first, second);
        await db.SaveChangesAsync();
        var service = new CommunicationHubService(db, new TestAuditEventWriter());

        var direct = await service.CreateAsync(organization.Id, first.Id,
            new CreateCommunicationChatRequest(null, null, true, true, [second.Id]));
        var group = await service.CreateAsync(organization.Id, first.Id,
            new CreateCommunicationChatRequest("Unauthorized", null, false, false, [second.Id]));

        Assert.True(direct.Succeeded);
        Assert.Equal("Second", direct.Chat!.Title);
        Assert.False(group.Succeeded);
        Assert.Equal("not_authorized", group.ErrorCode);
    }

    private static Organization Organization() => new() { Id = Guid.NewGuid(), Name = "Example",
        Status = OrganizationStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
    private static OrganizationUser User(Guid organizationId, string name, OrganizationPermissionLevel permission, Guid? roleId = null) => new()
    {
        Id = Guid.NewGuid(), OrganizationId = organizationId, DisplayName = name, RoleId = roleId,
        EmployeeType = EmployeeType.Human, PermissionLevel = permission, IsActive = true, CreatedAt = DateTimeOffset.UtcNow
    };
    private static CSweetDbContext CreateDb() => new(new DbContextOptionsBuilder<CSweetDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
