using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSweet.Communications.Abstractions;
using CSweet.Communications.Discord;
using CSweet.DiscordRelay;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("relay-postgres")
    ?? builder.Configuration.GetConnectionString("RelayPostgres");
builder.Services.AddDbContextFactory<RelayDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString)) options.UseNpgsql(connectionString);
    else options.UseInMemoryDatabase("CSweetDiscordRelay");
});
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddDataProtection().SetApplicationName("CSweet.DiscordRelay");
var botToken = builder.Configuration["Discord:Token"];
if (!string.IsNullOrWhiteSpace(botToken))
{
    builder.Services.AddDiscordGateway(options =>
    {
        options.Token = botToken;
        options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent;
    }).AddGatewayHandlers(typeof(Program).Assembly);
}

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<RelayDbContext>>().CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.MapHealthChecks("/health");
app.MapGet("/api/v1/status", () => new { service = "CSweet.DiscordRelay", gatewayConfigured = !string.IsNullOrWhiteSpace(botToken) });

app.MapPost("/api/v1/pairings", async (CreatePairingRequest request, HttpContext http, IDbContextFactory<RelayDbContext> factory) =>
{
    if (!FixedEquals(http.Request.Headers["X-Relay-Admin-Key"].ToString(), builder.Configuration["Relay:AdminKey"])) return Results.Unauthorized();
    var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    var now = DateTimeOffset.UtcNow;
    await using var db = await factory.CreateDbContextAsync();
    var pairing = new RelayPairing { Id = Guid.NewGuid(), OrganizationKey = request.OrganizationKey, AccessTokenHash = Hash(token), CreatedAt = now, UpdatedAt = now };
    db.Pairings.Add(pairing); await db.SaveChangesAsync();
    return Results.Created($"/api/v1/pairings/{pairing.Id}", new { pairing.Id, accessToken = token });
});

app.MapGet("/api/v1/oauth/install", async (Guid pairingId, string guildId, string redirectUri, HttpContext http,
    IDbContextFactory<RelayDbContext> factory, IDataProtectionProvider protection) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var accessTokenHash = Hash(Bearer(http));
    var pairing = await db.Pairings.SingleOrDefaultAsync(x => x.Id == pairingId && x.AccessTokenHash == accessTokenHash);
    if (pairing is null) return Results.Unauthorized();
    var applicationId = builder.Configuration.GetValue<ulong>("Discord:ApplicationId");
    if (applicationId == 0 || !Uri.TryCreate(redirectUri, UriKind.Absolute, out var callback))
        return Results.BadRequest("Discord application configuration and an absolute callback URL are required.");
    var state = protection.CreateProtector("DiscordOAuthState.v1").Protect(JsonSerializer.Serialize(
        new DiscordInstallState(pairingId, guildId, callback.ToString(), DateTimeOffset.UtcNow.AddMinutes(10))));
    var uri = DiscordOAuthUrlBuilder.BuildInstallUri(applicationId, callback, state);
    return Results.Ok(new { installUrl = uri.ToString() + $"&guild_id={guildId}&disable_guild_select=true" });
});

app.MapGet("/api/v1/oauth/callback", async (string code, string state, IDataProtectionProvider protection,
    IHttpClientFactory clients, IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    DiscordInstallState? install;
    try
    {
        var json = protection.CreateProtector("DiscordOAuthState.v1").Unprotect(state);
        install = JsonSerializer.Deserialize<DiscordInstallState>(json);
    }
    catch { return Results.BadRequest("The Discord installation state is invalid."); }
    if (install is null || install.ExpiresAt < DateTimeOffset.UtcNow) return Results.BadRequest("The Discord installation state expired.");
    var clientId = builder.Configuration["Discord:ApplicationId"];
    var clientSecret = builder.Configuration["Discord:ClientSecret"];
    if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        return Results.Problem("Discord OAuth is not configured.", statusCode: 503);
    using var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/v10/oauth2/token")
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId, ["client_secret"] = clientSecret, ["grant_type"] = "authorization_code",
            ["code"] = code, ["redirect_uri"] = install.RedirectUri
        })
    };
    using var response = await clients.CreateClient().SendAsync(request, cancellationToken);
    if (!response.IsSuccessStatusCode) return Results.BadRequest("Discord rejected the installation authorization.");
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var pairing = await db.Pairings.SingleOrDefaultAsync(x => x.Id == install.PairingId, cancellationToken);
    if (pairing is null) return Results.NotFound();
    pairing.GuildId = install.GuildId; pairing.IsPaused = false; pairing.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return Results.Text("Discord is connected to C-Sweet. You may close this window.", "text/plain");
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/activate", async (Guid pairingId, ActivatePairingRequest request,
    HttpContext http, IDbContextFactory<RelayDbContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var accessTokenHash = Hash(Bearer(http));
    var pairing = await db.Pairings.SingleOrDefaultAsync(x => x.Id == pairingId && x.AccessTokenHash == accessTokenHash);
    if (pairing is null) return Results.Unauthorized();
    pairing.GuildId = request.GuildId; pairing.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/v1/pairings/{pairingId:guid}/inbound", async (Guid pairingId, HttpContext http,
    IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    var envelopes = await db.Envelopes.Where(x => x.PairingId == pairingId && x.AcknowledgedAt == null && x.AvailableAt <= DateTimeOffset.UtcNow)
        .OrderBy(x => x.CreatedAt).Take(100).Select(x => x.PayloadJson).ToListAsync(cancellationToken);
    return Results.Ok(envelopes.Select(x => JsonSerializer.Deserialize<NormalizedCommunicationEnvelope>(x)).Where(x => x is not null));
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/inbound/{envelopeId:guid}/ack", async (Guid pairingId, Guid envelopeId,
    HttpContext http, IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    var envelope = await db.Envelopes.SingleOrDefaultAsync(x => x.PairingId == pairingId && x.Id == envelopeId, cancellationToken);
    if (envelope is null) return Results.NotFound();
    envelope.AcknowledgedAt = DateTimeOffset.UtcNow;
    envelope.PayloadJson = "{}";
    await db.SaveChangesAsync(cancellationToken); return Results.NoContent();
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/provision", async (Guid pairingId, WorkspaceProvisioningPlan plan,
    HttpContext http, IDbContextFactory<RelayDbContext> factory, IDataProtectionProvider protection, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(botToken)) return Results.Problem("Discord gateway is not configured.", statusCode: 503);
    using var client = DiscordApiClient.CreateHttpClient(botToken);
    var result = await new DiscordWorkspaceProvisioner(new DiscordApiClient(client)).ApplyAsync(plan, cancellationToken);
    var sanitized = new List<ProviderResourceDescriptor>();
    var protector = protection.CreateProtector("DiscordWebhookTokens.v1");
    foreach (var resource in result.Resources)
    {
        var parts = resource.Kind == CommunicationResourceKind.Webhook ? resource.ExternalId.Split('|', 2) : [];
        if (parts.Length == 2 && resource.ParentExternalId is not null)
        {
            var secret = await db.WebhookSecrets.SingleOrDefaultAsync(x => x.PairingId == pairingId && x.ChannelExternalId == resource.ParentExternalId, cancellationToken);
            secret ??= new RelayWebhookSecret { Id = Guid.NewGuid(), PairingId = pairingId, ChannelExternalId = resource.ParentExternalId };
            if (db.Entry(secret).State == EntityState.Detached) db.WebhookSecrets.Add(secret);
            secret.WebhookExternalId = parts[0]; secret.TokenCiphertext = protector.Protect(parts[1]); secret.UpdatedAt = DateTimeOffset.UtcNow;
            sanitized.Add(resource with { ExternalId = parts[0] });
        }
        else sanitized.Add(resource);
    }
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(result with { Resources = sanitized });
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/outbound", async (Guid pairingId, OutboundCommunicationEnvelope envelope,
    HttpContext http, IDbContextFactory<RelayDbContext> factory, IDataProtectionProvider protection, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(botToken)) return Results.Problem("Discord gateway is not configured.", statusCode: 503);
    var receipt = await db.OutboundReceipts.SingleOrDefaultAsync(x => x.PairingId == pairingId && x.IdempotencyKey == envelope.IdempotencyKey, cancellationToken);
    if (receipt is not null) return Results.Ok(JsonSerializer.Deserialize<CommunicationResult>(receipt.ResultJson));
    using var client = DiscordApiClient.CreateHttpClient(botToken);
    var discord = new DiscordApiClient(client);
    var webhook = await db.WebhookSecrets.SingleOrDefaultAsync(x => x.PairingId == pairingId && x.ChannelExternalId == envelope.DestinationExternalId, cancellationToken);
    var result = webhook is not null && !string.IsNullOrWhiteSpace(envelope.PersonaName)
        ? await discord.SendWebhookAsync(webhook.WebhookExternalId,
            protection.CreateProtector("DiscordWebhookTokens.v1").Unprotect(webhook.TokenCiphertext), envelope, cancellationToken)
        : await discord.SendAsync(envelope, cancellationToken);
    db.OutboundReceipts.Add(new RelayOutboundReceipt
    {
        Id = Guid.NewGuid(), PairingId = pairingId, IdempotencyKey = envelope.IdempotencyKey,
        ResultJson = JsonSerializer.Serialize(result), CreatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync(cancellationToken);
    return Results.Ok(result);
});

app.MapPost("/api/v1/admin/pairings/{pairingId:guid}/pause", async (Guid pairingId, bool paused, HttpContext http,
    IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    if (!FixedEquals(http.Request.Headers["X-Relay-Admin-Key"].ToString(), builder.Configuration["Relay:AdminKey"])) return Results.Unauthorized();
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var pairing = await db.Pairings.SingleOrDefaultAsync(x => x.Id == pairingId, cancellationToken);
    if (pairing is null) return Results.NotFound();
    pairing.IsPaused = paused; pairing.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

app.MapGet("/metrics", async (IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    var active = await db.Pairings.CountAsync(x => !x.IsPaused, cancellationToken);
    var pending = await db.Envelopes.CountAsync(x => x.AcknowledgedAt == null, cancellationToken);
    return Results.Text($"csweet_discord_relay_active_pairings {active}\ncsweet_discord_relay_pending_envelopes {pending}\n", "text/plain; version=0.0.4");
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/link-codes", async (Guid pairingId, RegisterLinkCodeRequest request,
    HttpContext http, IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    db.LinkCodes.Add(new RelayLinkCode
    {
        Id = Guid.NewGuid(), PairingId = pairingId, CodeHash = Hash(request.Code.Trim().ToUpperInvariant()), ExpiresAt = request.ExpiresAt
    });
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
});

app.MapPost("/api/v1/pairings/{pairingId:guid}/members/assign", async (Guid pairingId, AssignRelayMemberRequest request,
    HttpContext http, IDbContextFactory<RelayDbContext> factory, CancellationToken cancellationToken) =>
{
    await using var db = await factory.CreateDbContextAsync(cancellationToken);
    if (!await Authorized(db, pairingId, Bearer(http), cancellationToken)) return Results.Unauthorized();
    var pairing = await db.Pairings.SingleAsync(x => x.Id == pairingId, cancellationToken);
    if (pairing.GuildId != request.WorkspaceExternalId) return Results.BadRequest("The guild does not belong to this pairing.");
    if (string.IsNullOrWhiteSpace(botToken)) return Results.Problem("Discord gateway is not configured.", statusCode: 503);
    using var client = DiscordApiClient.CreateHttpClient(botToken);
    return Results.Ok(await new DiscordApiClient(client).AssignMemberAsync(request.WorkspaceExternalId,
        request.ExternalUserId, request.MemberRoleExternalId, cancellationToken));
});

app.Run();

static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
static string Bearer(HttpContext http) => http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
    ? http.Request.Headers.Authorization.ToString()[7..] : string.Empty;
static bool FixedEquals(string supplied, string? expected) => !string.IsNullOrWhiteSpace(expected) &&
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
static async Task<bool> Authorized(RelayDbContext db, Guid pairingId, string token, CancellationToken cancellationToken)
{
    if (string.IsNullOrEmpty(token)) return false;
    var tokenHash = Hash(token);
    return await db.Pairings.AnyAsync(x => x.Id == pairingId && x.AccessTokenHash == tokenHash && !x.IsPaused, cancellationToken);
}

public sealed record CreatePairingRequest(string OrganizationKey);
public sealed record ActivatePairingRequest(string GuildId);
public sealed record RegisterLinkCodeRequest(string Code, DateTimeOffset ExpiresAt);
public sealed record DiscordInstallState(Guid PairingId, string GuildId, string RedirectUri, DateTimeOffset ExpiresAt);
public sealed record AssignRelayMemberRequest(string WorkspaceExternalId, string ExternalUserId, string MemberRoleExternalId);
public partial class Program;
