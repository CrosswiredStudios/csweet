using CSweet.Communications.Abstractions;
using CSweet.Domain.Communications;
using CSweet.Infrastructure.Communications;
using CSweet.Infrastructure.Persistence;
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
            Id = Guid.NewGuid(), OrganizationId = organizationId, Provider = CommunicationProviderKind.Discord,
            WorkspaceExternalId = "123", WorkspaceMode = CommunicationWorkspaceMode.Contained,
            Status = CommunicationConnectionStatus.Connected, RelayPairingId = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new CommunicationWorkspaceService(db, new TestAuditEventWriter(), new NoOpRelay());
        var result = await service.DisconnectDiscordAsync(organizationId);

        Assert.True(result.Succeeded);
        Assert.Equal(CommunicationConnectionStatus.Paused, (await db.CommunicationConnections.SingleAsync()).Status);
        Assert.Equal(CommunicationDeliveryKind.DisconnectWorkspace, (await db.CommunicationDeliveries.SingleAsync()).Kind);
    }

    private sealed class NoOpRelay : ICommunicationRelayClient
    {
        public async IAsyncEnumerable<NormalizedCommunicationEnvelope> ReadInboundAsync(Guid pairingId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default) { await Task.CompletedTask; yield break; }
        public Task AcknowledgeAsync(Guid pairingId, Guid envelopeId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CommunicationResult> SendAsync(Guid pairingId, OutboundCommunicationEnvelope envelope, CancellationToken cancellationToken = default) => Task.FromResult(CommunicationResult.Success());
        public Task<WorkspaceProvisioningResult> ApplyProvisioningAsync(Guid pairingId, WorkspaceProvisioningPlan plan, CancellationToken cancellationToken = default) => Task.FromResult(new WorkspaceProvisioningResult(true, [], []));
        public Task RegisterLinkCodeAsync(Guid pairingId, string code, DateTimeOffset expiresAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<CommunicationResult> AssignMemberAsync(Guid pairingId, string workspaceExternalId, string externalUserId, string memberRoleExternalId, CancellationToken cancellationToken = default) => Task.FromResult(CommunicationResult.Success());
    }
}
