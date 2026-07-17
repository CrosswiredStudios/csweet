using CSweet.Domain.Communications;
using CSweet.Infrastructure.Communications;
using CSweet.Infrastructure.Persistence;
using CSweet.Application.Setup;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public sealed class CommunicationWorkspaceServiceTests
{
    [Fact]
    public async Task Disconnect_PausesInboundAndQueuesManagedArchival()
    {
        await using var db = new CSweetDbContext(new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var organizationId = Guid.NewGuid();
        db.CommunicationConnections.Add(new CommunicationConnection
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, ProviderKey = CommunicationProviderKeys.Discord,
            WorkspaceExternalId = "123", WorkspaceMode = CommunicationWorkspaceMode.Contained,
            Status = CommunicationConnectionStatus.Connected, PluginInstallationId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CommunicationWorkspaceService(db, new TestAuditEventWriter(), new AllowPlugin());
        var result = await service.DisconnectDiscordAsync(organizationId);

        Assert.True(result.Succeeded);
        Assert.Equal(CommunicationConnectionStatus.Paused, (await db.CommunicationConnections.SingleAsync()).Status);
        Assert.Equal(CommunicationDeliveryKind.DisconnectWorkspace, (await db.CommunicationDeliveries.SingleAsync()).Kind);
    }

    private sealed class AllowPlugin : IPluginAuthorizationPolicy
    {
        public Task<bool> CanAccessOrganizationAsync(Guid installationId, Guid organizationId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
