using CSweet.Application.Core;
using CSweet.Application.Setup;
using CSweet.Contracts.Core;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class ConversationServiceTests
{
    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_SucceedsForAgentEmployee()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "Personal Assistant",
            Description = "Test agent",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Personal Assistant",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Conversation);
        Assert.Equal(agentUser.Id, result.Conversation.AgentOrganizationUserId);
        Assert.Equal(selfUser.Id, result.Conversation.InitiatedByOrganizationUserId);

        var persisted = await dbContext.CoreConversations.SingleAsync(x => x.Id == result.Conversation!.Id);
        Assert.Null(persisted.Title);
    }

    [Fact]
    public async Task StartAsync_RejectsHumanWithNotAnAgent()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(selfUser.Id);
        var result = await service.StartAsync(org.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("not_an_agent", result.ErrorCode);
    }

    [Fact]
    public async Task StartAsync_ReturnsNotFoundForUnknownId()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);
        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(Guid.NewGuid());
        var result = await service.StartAsync(org.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("agent_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task StartAsync_ReturnsNotFoundForAgentInDifferentOrg()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org1 = CreateOrganization();
        var org2 = CreateOrganization();
        dbContext.CoreOrganizations.AddRange(org1, org2);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org2.Id,
            DisplayName = "Agent",
            EmployeeType = EmployeeType.Agent,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org1.Id, request);

        Assert.False(result.Succeeded);
        Assert.Equal("agent_not_found", result.ErrorCode);
    }

    #endregion

    #region AppendMessageAsync Tests

    [Fact]
    public async Task AppendMessageAsync_PersistsAndOrdersByCreatedAt()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        var msg1 = await service.AppendMessageAsync(conversationId, ConversationRole.User, "Hello");
        await Task.Delay(5);
        var msg2 = await service.AppendMessageAsync(conversationId, ConversationRole.Assistant, "Hi there!");
        await Task.Delay(5);
        var msg3 = await service.AppendMessageAsync(conversationId, ConversationRole.User, "How are you?");

        Assert.Equal("Hello", msg1.Content);
        Assert.Equal("Hi there!", msg2.Content);
        Assert.Equal("How are you?", msg3.Content);

        var messages = await service.ListMessagesAsync(conversationId);
        Assert.Equal(3, messages.Count);
        Assert.Equal("Hello", messages[0].Content);
        Assert.Equal("Hi there!", messages[1].Content);
        Assert.Equal("How are you?", messages[2].Content);
    }

    [Fact]
    public async Task AppendMessageAsync_SetsTitleFromFirstUserMessage()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        // First user message sets the title
        await service.AppendMessageAsync(conversationId, ConversationRole.User, "Help me with marketing plan");

        var updated = await dbContext.CoreConversations.SingleAsync(x => x.Id == conversationId);
        Assert.Equal("Help me with marketing plan", updated.Title);

        // Assistant message should not change the title
        await service.AppendMessageAsync(conversationId, ConversationRole.Assistant, "Sure, I can help!");

        updated = await dbContext.CoreConversations.SingleAsync(x => x.Id == conversationId);
        Assert.Equal("Help me with marketing plan", updated.Title);
    }

    [Fact]
    public async Task AppendMessageAsync_TruncatesLongTitleTo80Chars()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        var longMessage = new string('A', 100);
        await service.AppendMessageAsync(conversationId, ConversationRole.User, longMessage);

        var updated = await dbContext.CoreConversations.SingleAsync(x => x.Id == conversationId);
        Assert.NotNull(updated.Title);
        Assert.Equal(80, updated.Title!.Length);
    }

    #endregion

    #region ListMessagesAsync Tests

    [Fact]
    public async Task ListAsync_ReturnsOnlyAgentConversationsInMostRecentOrder()
    {
        await using var dbContext = CreateDbContext();
        var service = new ConversationService(dbContext, new TestAuditEventWriter());
        var organization = CreateOrganization();
        var otherOrganization = CreateOrganization();
        var agentId = Guid.NewGuid();
        var otherAgentId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        dbContext.CoreOrganizations.AddRange(organization, otherOrganization);
        dbContext.CoreConversations.AddRange(
            new Conversation { Id = Guid.NewGuid(), OrganizationId = organization.Id, AgentOrganizationUserId = agentId, InitiatedByOrganizationUserId = Guid.NewGuid(), Title = "Older", CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-2) },
            new Conversation { Id = Guid.NewGuid(), OrganizationId = organization.Id, AgentOrganizationUserId = agentId, InitiatedByOrganizationUserId = Guid.NewGuid(), Title = "Newest", CreatedAt = now.AddHours(-1), UpdatedAt = now },
            new Conversation { Id = Guid.NewGuid(), OrganizationId = organization.Id, AgentOrganizationUserId = otherAgentId, InitiatedByOrganizationUserId = Guid.NewGuid(), Title = "Other agent", CreatedAt = now, UpdatedAt = now },
            new Conversation { Id = Guid.NewGuid(), OrganizationId = otherOrganization.Id, AgentOrganizationUserId = agentId, InitiatedByOrganizationUserId = Guid.NewGuid(), Title = "Other organization", CreatedAt = now, UpdatedAt = now });
        await dbContext.SaveChangesAsync();

        var conversations = await service.ListAsync(organization.Id, agentId);

        Assert.Equal(2, conversations.Count);
        Assert.Equal("Newest", conversations[0].Title);
        Assert.Equal("Older", conversations[1].Title);
    }

    [Fact]
    public async Task ListMessagesAsync_ReturnsChronologicalOrder()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        // Add messages with small delays to ensure distinct timestamps
        await service.AppendMessageAsync(conversationId, ConversationRole.User, "First");
        await Task.Delay(5);
        await service.AppendMessageAsync(conversationId, ConversationRole.Assistant, "Second");
        await Task.Delay(5);
        await service.AppendMessageAsync(conversationId, ConversationRole.User, "Third");

        var messages = await service.ListMessagesAsync(conversationId);

        Assert.Equal(3, messages.Count);
        Assert.True(messages[0].CreatedAt <= messages[1].CreatedAt);
        Assert.True(messages[1].CreatedAt <= messages[2].CreatedAt);
    }

    [Fact]
    public async Task ListMessagesAsync_ReturnsEmptyForNewConversation()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        var messages = await service.ListMessagesAsync(conversationId);
        Assert.Empty(messages);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_ReturnsConversation()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var org = CreateOrganization();
        dbContext.CoreOrganizations.Add(org);

        var selfUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "Self",
            EmployeeType = EmployeeType.Human,
            PermissionLevel = OrganizationPermissionLevel.Owner,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreOrganizationUsers.Add(selfUser);

        var agentWorker = new Worker
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            Name = "PA",
            Description = "Test",
            WorkerType = WorkerType.LocalAgent,
            ExecutionMode = WorkerExecutionMode.InProcess,
            CapabilitiesJson = "[]",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.CoreWorkers.Add(agentWorker);

        var agentUser = new OrganizationUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            DisplayName = "PA",
            EmployeeType = EmployeeType.Agent,
            WorkerId = agentWorker.Id,
            PermissionLevel = OrganizationPermissionLevel.Viewer,
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(1)
        };
        dbContext.CoreOrganizationUsers.Add(agentUser);

        await dbContext.SaveChangesAsync();

        var request = new StartConversationRequest(agentUser.Id);
        var result = await service.StartAsync(org.Id, request);
        Assert.True(result.Succeeded);
        var conversationId = result.Conversation!.Id;

        var retrieved = await service.GetAsync(conversationId);
        Assert.NotNull(retrieved);
        Assert.Equal(conversationId, retrieved.Id);
        Assert.Equal(agentUser.Id, retrieved.AgentOrganizationUserId);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForUnknownId()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        var retrieved = await service.GetAsync(Guid.NewGuid());
        Assert.Null(retrieved);
    }

    #endregion

    #region Provider Selection Tests

    [Fact]
    public async Task GetDefaultProviderProfileIdAsync_ReturnsFirstEnabledProvider()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);
        var disabled = CreateProvider(DateTimeOffset.UtcNow, isEnabled: false);
        var firstEnabled = CreateProvider(DateTimeOffset.UtcNow.AddSeconds(1), isEnabled: true);
        var secondEnabled = CreateProvider(DateTimeOffset.UtcNow.AddSeconds(2), isEnabled: true);

        dbContext.LlmProviderProfiles.AddRange(disabled, secondEnabled, firstEnabled);
        await dbContext.SaveChangesAsync();

        var providerId = await service.GetDefaultProviderProfileIdAsync();

        Assert.Equal(firstEnabled.Id, providerId);
    }

    [Fact]
    public async Task GetDefaultProviderProfileIdAsync_ReturnsNullWhenNoProviderIsEnabled()
    {
        await using var dbContext = CreateDbContext();
        var auditWriter = new TestAuditEventWriter();
        var service = new ConversationService(dbContext, auditWriter);

        dbContext.LlmProviderProfiles.Add(CreateProvider(DateTimeOffset.UtcNow, isEnabled: false));
        await dbContext.SaveChangesAsync();

        var providerId = await service.GetDefaultProviderProfileIdAsync();

        Assert.Null(providerId);
    }

    #endregion

    #region Helpers

    private static DbContextOptions<CSweetDbContext> CreateDbContextOptions()
    {
        return new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    private static CSweetDbContext CreateDbContext()
    {
        return new CSweetDbContext(CreateDbContextOptions());
    }

    private static Organization CreateOrganization()
    {
        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    private static LlmProviderProfile CreateProvider(DateTimeOffset createdAt, bool isEnabled)
    {
        return new LlmProviderProfile
        {
            Id = Guid.NewGuid(),
            Name = $"Provider {Guid.NewGuid():N}",
            ProviderType = LlmProviderType.LmStudio,
            BaseUrl = "http://localhost:1234",
            DefaultChatModel = "local-model",
            SupportsStreaming = true,
            IsEnabled = isEnabled,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };
    }

    #endregion
}
