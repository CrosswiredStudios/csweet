using CSweet.AI.Providers;
using CSweet.Domain.Core;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Core;
using CSweet.Infrastructure.Persistence;
using CSweet.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CSweet.UnitTests;

public sealed class AgentMemoryServiceTests
{
    [Fact]
    public async Task RelationshipMemory_IsRecalledAcrossConversationsAndBrowsable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"csweet-agent-memory-{Guid.NewGuid():N}.db");
        await using var store = new SqliteMemoryStore(path);
        await using var db = new CSweetDbContext(new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        try
        {
            var organizationId = Guid.NewGuid();
            var humanId = Guid.NewGuid();
            var otherHumanId = Guid.NewGuid();
            var applicationUserId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();
            var installationId = Guid.NewGuid();
            var packageId = Guid.NewGuid();
            db.CoreOrganizations.Add(new Organization { Id = organizationId, Name = "Test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            db.CoreOrganizationUsers.Add(new OrganizationUser { Id = humanId, OrganizationId = organizationId, ApplicationUserId = applicationUserId, DisplayName = "Owner", EmployeeType = EmployeeType.Human, CreatedAt = DateTimeOffset.UtcNow });
            db.CoreOrganizationUsers.Add(new OrganizationUser { Id = otherHumanId, OrganizationId = organizationId, DisplayName = "Other", EmployeeType = EmployeeType.Human, CreatedAt = DateTimeOffset.UtcNow });
            var package = new AgentPackageVersion { Id = packageId, AgentId = "com.example.assistant", AgentName = "Assistant", Version = "1.0.0" };
            var installation = new AgentInstallation { Id = installationId, PackageVersionId = packageId, PackageVersion = package, BusinessId = organizationId.ToString(), IsEnabled = true };
            var employee = new OrganizationUser { Id = employeeId, OrganizationId = organizationId, DisplayName = "Assistant", EmployeeType = EmployeeType.Agent, AgentInstallationId = installationId, AgentInstallation = installation, CreatedAt = DateTimeOffset.UtcNow };
            db.AgentPackageVersions.Add(package);
            db.AgentInstallations.Add(installation);
            db.CoreOrganizationUsers.Add(employee);
            var first = new Conversation { Id = Guid.NewGuid(), OrganizationId = organizationId, AgentOrganizationUserId = employeeId, InitiatedByOrganizationUserId = humanId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var second = new Conversation { Id = Guid.NewGuid(), OrganizationId = organizationId, AgentOrganizationUserId = employeeId, InitiatedByOrganizationUserId = humanId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var otherRelationship = new Conversation { Id = Guid.NewGuid(), OrganizationId = organizationId, AgentOrganizationUserId = employeeId, InitiatedByOrganizationUserId = otherHumanId, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var message = new ConversationMessage { Id = Guid.NewGuid(), ConversationId = first.Id, Conversation = first, Role = ConversationRole.User, Content = "My name is Alice.", CreatedAt = DateTimeOffset.UtcNow };
            db.CoreConversations.AddRange(first, second, otherRelationship);
            db.CoreConversationMessages.Add(message);
            db.MemoryCaptureOutbox.Add(new MemoryCaptureOutboxItem { Id = Guid.NewGuid(), ConversationMessageId = message.Id, Status = MemoryCaptureStatus.Pending, CreatedAt = DateTimeOffset.UtcNow, NextAttemptAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();

            var service = new AgentMemoryService(db, store, new ThrowingProviderFactory(), NullLogger<AgentMemoryService>.Instance);
            await service.CaptureMessageAsync(message.Id);

            var recalled = await service.RecallForConversationAsync(second.Id, "What is my name?");
            var isolatedRecall = await service.RecallForConversationAsync(otherRelationship.Id, "What is my name?");
            var summary = await service.GetSummaryAsync(organizationId, employeeId);
            var page = await service.BrowseAsync(organizationId, employeeId, new(Limit: 20));
            var detail = await service.GetItemAsync(organizationId, employeeId, message.Id);

            Assert.Contains("Alice", recalled);
            Assert.Null(isolatedRecall);
            Assert.True(await service.CanExploreAsync(organizationId, applicationUserId));
            Assert.False(await service.CanExploreAsync(organizationId, Guid.NewGuid()));
            Assert.Equal(1, summary!.EpisodeCount);
            Assert.Contains(page!.Items, item => item.Id == message.Id && item.ConversationId == first.Id);
            Assert.Contains(detail!.RecallUses!, use => use.ConversationId == second.Id && use.UserId == humanId);
            var registered = Assert.Single(await db.AgentMemoryNamespaces.ToListAsync());
            Assert.Equal(employeeId, registered.EmployeeId);
            Assert.Equal(humanId, registered.UserId);
        }
        finally
        {
            foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }

    private sealed class ThrowingProviderFactory : ILlmProviderFactory
    {
        public Task<IChatClient> CreateChatClientAsync(Guid providerProfileId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Enrichment is not used by this test.");
    }
}
