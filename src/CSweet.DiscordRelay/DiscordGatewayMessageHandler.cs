using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using CSweet.Communications.Abstractions;
using Microsoft.EntityFrameworkCore;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace CSweet.DiscordRelay;

public sealed class DiscordGatewayMessageHandler(IDbContextFactory<RelayDbContext> dbFactory) : IMessageCreateGatewayHandler
{
    public async ValueTask HandleAsync(Message message)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        RelayPairing? pairing;
        if (message.GuildId.HasValue)
            pairing = await db.Pairings.SingleOrDefaultAsync(x => x.GuildId == message.GuildId.Value.ToString() && !x.IsPaused);
        else
            pairing = await ResolveDirectPairingAsync(db, message);
        if (pairing is null) return;

        var envelope = new NormalizedCommunicationEnvelope(
            Guid.NewGuid(), "Discord", CommunicationEnvelopeKind.Message,
            message.GuildId?.ToString() ?? pairing.GuildId ?? string.Empty,
            message.ChannelId.ToString(), null, message.Author.Id.ToString(), message.Id.ToString(),
            message.ReferencedMessage?.Id.ToString(), message.Content,
            message.MentionedRoleIds.Select(x => x.ToString()).ToArray(), message.Author.IsBot, message.WebhookId.HasValue,
            message.CreatedAt, $"discord:{message.Id}",
            new Dictionary<string, string> { ["isDirect"] = (!message.GuildId.HasValue).ToString() });
        db.Envelopes.Add(new RelayEnvelope
        {
            Id = envelope.Id, PairingId = pairing.Id, IdempotencyKey = envelope.IdempotencyKey,
            PayloadJson = JsonSerializer.Serialize(envelope), CreatedAt = DateTimeOffset.UtcNow, AvailableAt = DateTimeOffset.UtcNow
        });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* Gateway replay: unique idempotency key already persisted. */ }
    }

    private static async Task<RelayPairing?> ResolveDirectPairingAsync(RelayDbContext db, Message message)
    {
        if (message.Content.StartsWith("/link ", StringComparison.OrdinalIgnoreCase))
        {
            var code = message.Content[6..].Trim().ToUpperInvariant();
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
            var now = DateTimeOffset.UtcNow;
            var linkCode = await db.LinkCodes.SingleOrDefaultAsync(x => x.CodeHash == hash && x.RedeemedAt == null && x.ExpiresAt > now);
            if (linkCode is null) return null;
            linkCode.RedeemedAt = now;
            var route = await db.DirectRoutes.SingleOrDefaultAsync(x => x.ExternalUserId == message.Author.Id.ToString());
            if (route is null) db.DirectRoutes.Add(new RelayDirectRoute { Id = Guid.NewGuid(), PairingId = linkCode.PairingId, ExternalUserId = message.Author.Id.ToString(), CreatedAt = now });
            else route.PairingId = linkCode.PairingId;
            await db.SaveChangesAsync();
            return await db.Pairings.SingleOrDefaultAsync(x => x.Id == linkCode.PairingId && !x.IsPaused);
        }
        var direct = await db.DirectRoutes.SingleOrDefaultAsync(x => x.ExternalUserId == message.Author.Id.ToString());
        return direct is null ? null : await db.Pairings.SingleOrDefaultAsync(x => x.Id == direct.PairingId && !x.IsPaused);
    }
}
