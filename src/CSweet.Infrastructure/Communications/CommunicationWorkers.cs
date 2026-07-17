using CSweet.Application.Communications;
using CSweet.Communications.Abstractions;
using CSweet.Contracts.Communications;
using CSweet.Domain.Communications;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CSweet.Infrastructure.Communications;

public sealed class CommunicationDeliveryWorker(IServiceScopeFactory scopeFactory, ILogger<CommunicationDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processed = await ProcessOneAsync(scope.ServiceProvider, stoppingToken);
                if (!processed) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Communication delivery processing failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private static async Task<bool> ProcessOneAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<CSweetDbContext>();
        var now = DateTimeOffset.UtcNow;
        var delivery = await db.CommunicationDeliveries.Where(x =>
                (x.Status == CommunicationDeliveryStatus.Pending && x.NextAttemptAt <= now) ||
                (x.Status == CommunicationDeliveryStatus.Leased && x.LeaseUntil < now))
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (delivery is null) return false;
        delivery.Status = CommunicationDeliveryStatus.Leased; delivery.LeaseOwner = Environment.MachineName;
        delivery.LeaseUntil = now.AddMinutes(2); delivery.Attempts++; delivery.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var connection = delivery.ConnectionId.HasValue
                ? await db.CommunicationConnections.SingleOrDefaultAsync(x => x.Id == delivery.ConnectionId, cancellationToken)
                : await db.CommunicationConnections.SingleOrDefaultAsync(x => x.OrganizationId == delivery.OrganizationId && x.Provider == CommunicationProviderKind.Discord, cancellationToken);
            if (connection is null)
            {
                // Employee desired state is retained in the employee row; connecting later queues a full reconciliation.
                delivery.Status = CommunicationDeliveryStatus.Delivered; delivery.LastError = "No external workspace is connected.";
            }
            else if (!Guid.TryParse(connection.RelayPairingId, out var pairingId))
                throw new InvalidOperationException("The Discord connection has no valid relay pairing ID.");
            else
            {
                var relay = services.GetRequiredService<ICommunicationRelayClient>();
                if (delivery.Kind == CommunicationDeliveryKind.SendMessage)
                {
                    var envelope = JsonSerializer.Deserialize<OutboundCommunicationEnvelope>(delivery.PayloadJson)
                        ?? throw new InvalidOperationException("The outbound communication payload is invalid.");
                    var send = await relay.SendAsync(pairingId, envelope, cancellationToken);
                    if (!send.Succeeded) throw new InvalidOperationException(send.Error?.Message ?? "Discord delivery failed.");
                    delivery.ExternalReceiptId = send.ExternalId;
                    if (delivery.ConversationMessageId.HasValue && send.ExternalId is not null)
                    {
                        db.ExternalMessageReferences.Add(new ExternalMessageReference
                        {
                            Id = Guid.NewGuid(), ConnectionId = connection.Id, ConversationMessageId = delivery.ConversationMessageId,
                            ChannelExternalId = envelope.DestinationExternalId, MessageExternalId = send.ExternalId,
                            ThreadExternalId = envelope.ThreadExternalId, IsInbound = false, CreatedAt = now
                        });
                    }
                }
                else
                {
                    var workspace = services.GetRequiredService<ICommunicationWorkspaceService>();
                    var plan = delivery.Kind == CommunicationDeliveryKind.DisconnectWorkspace
                        ? await BuildDisconnectPlanAsync(db, connection, cancellationToken)
                        : await workspace.PreviewAsync(connection.OrganizationId, cancellationToken)
                            ?? throw new InvalidOperationException("No provisioning plan is available.");
                    var result = await relay.ApplyProvisioningAsync(pairingId, plan, cancellationToken);
                    if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Message)));
                    foreach (var descriptor in result.Resources)
                    {
                        var resource = await db.ManagedExternalResources.SingleOrDefaultAsync(x => x.ConnectionId == connection.Id && x.Purpose == descriptor.Purpose, cancellationToken);
                        resource ??= new ManagedExternalResource { Id = Guid.NewGuid(), ConnectionId = connection.Id, OrganizationId = connection.OrganizationId, Purpose = descriptor.Purpose, CreatedAt = now };
                        if (db.Entry(resource).State == EntityState.Detached) db.ManagedExternalResources.Add(resource);
                        resource.Kind = Enum.Parse<ManagedResourceKind>(descriptor.Kind.ToString());
                        resource.ExternalId = descriptor.ExternalId; resource.ParentExternalId = descriptor.ParentExternalId;
                        resource.DisplayName = descriptor.Name;
                        resource.IsArchived = plan.Changes.FirstOrDefault(x => x.Purpose == descriptor.Purpose)?.Change == CommunicationChangeKind.Archive;
                        resource.UpdatedAt = now;
                        resource.OrganizationUserId = ParseEmployeeId(descriptor.Purpose);
                        resource.TeamId = ParseScopedId(descriptor.Purpose, "team");
                        resource.ProjectId = ParseScopedId(descriptor.Purpose, "project");
                    }
                    connection.Status = delivery.Kind == CommunicationDeliveryKind.DisconnectWorkspace
                        ? CommunicationConnectionStatus.Disconnected : CommunicationConnectionStatus.Connected;
                    connection.UpdatedAt = now;
                    if (delivery.Kind == CommunicationDeliveryKind.DisconnectWorkspace)
                    {
                        var links = await db.ExternalIdentityLinks.Where(x => x.ConnectionId == connection.Id && x.RevokedAt == null).ToListAsync(cancellationToken);
                        foreach (var link in links) { link.RevokedAt = now; link.ActiveDirectAgentOrganizationUserId = null; }
                    }
                }
                delivery.Status = CommunicationDeliveryStatus.Delivered; delivery.LastError = null;
            }
        }
        catch (Exception exception)
        {
            delivery.Status = delivery.Attempts >= 8 ? CommunicationDeliveryStatus.DeadLettered : CommunicationDeliveryStatus.Pending;
            delivery.NextAttemptAt = now.AddSeconds(Math.Min(900, Math.Pow(2, delivery.Attempts)) + Random.Shared.NextDouble() * 5);
            delivery.LastError = exception.Message;
        }
        delivery.LeaseOwner = null; delivery.LeaseUntil = null; delivery.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static Guid? ParseEmployeeId(string purpose)
    {
        var segments = purpose.Split(':');
        return segments.Length >= 3 && segments[0] == "agent" && Guid.TryParse(segments[1], out var id) ? id : null;
    }

    private static async Task<WorkspaceProvisioningPlan> BuildDisconnectPlanAsync(CSweetDbContext db,
        CommunicationConnection connection, CancellationToken cancellationToken)
    {
        var resources = await db.ManagedExternalResources.Where(x => x.ConnectionId == connection.Id && !x.IsArchived).ToListAsync(cancellationToken);
        return new WorkspaceProvisioningPlan(connection.OrganizationId, "Discord", connection.WorkspaceExternalId,
            resources.Select(x => new WorkspaceProvisioningChange(CommunicationChangeKind.Archive,
                Enum.Parse<CommunicationResourceKind>(x.Kind.ToString()), x.Purpose, x.DisplayName, x.ExternalId,
                "Disconnecting archives managed resources and preserves C-Sweet history.")).ToList(), DateTimeOffset.UtcNow);
    }

    private static Guid? ParseScopedId(string purpose, string scope)
    {
        var segments = purpose.Split(':');
        return segments.Length >= 3 && segments[0] == scope && Guid.TryParse(segments[1], out var id) ? id : null;
    }
}

public sealed class CommunicationInboundWorker(IServiceScopeFactory scopeFactory, ILogger<CommunicationInboundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
                var connections = await db.CommunicationConnections.AsNoTracking().Where(x => x.Provider == CommunicationProviderKind.Discord &&
                    x.Status != CommunicationConnectionStatus.Paused && x.RelayPairingId != null).ToListAsync(stoppingToken);
                foreach (var connection in connections)
                {
                    if (!Guid.TryParse(connection.RelayPairingId, out var pairingId)) continue;
                    var relay = scope.ServiceProvider.GetRequiredService<ICommunicationRelayClient>();
                    await foreach (var envelope in relay.ReadInboundAsync(pairingId, stoppingToken))
                    {
                        CommunicationActionResponse result;
                        if (envelope.Content?.StartsWith("/link ", StringComparison.OrdinalIgnoreCase) == true && envelope.SenderExternalId is not null)
                        {
                            var service = scope.ServiceProvider.GetRequiredService<ICommunicationWorkspaceService>();
                            var linked = await service.RedeemLinkCodeAsync(new RedeemExternalIdentityRequest(connection.WorkspaceExternalId,
                                envelope.SenderExternalId, envelope.Content[6..].Trim()), stoppingToken);
                            result = linked is null ? new(false, "invalid_link_code", "That link code is invalid or expired.") : new(true, null, "Discord account linked to C-Sweet.");
                        }
                        else result = await scope.ServiceProvider.GetRequiredService<ICommunicationRouter>().RouteInboundAsync(envelope, stoppingToken);

                        var isControlCommand = envelope.Content?.StartsWith("/talk", StringComparison.OrdinalIgnoreCase) == true ||
                                               envelope.Content?.StartsWith("/link", StringComparison.OrdinalIgnoreCase) == true;
                        if ((!result.Succeeded || isControlCommand) && envelope.ChannelExternalId is not null)
                        {
                            await relay.SendAsync(pairingId, new OutboundCommunicationEnvelope(Guid.NewGuid(), "Discord",
                                connection.WorkspaceExternalId, envelope.ChannelExternalId, result.Message, envelope.ThreadExternalId,
                                envelope.MessageExternalId, "C-Sweet", null, $"response:{envelope.Id:D}"), stoppingToken);
                        }
                        await relay.AcknowledgeAsync(pairingId, envelope.Id, stoppingToken);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Communication inbound polling failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
