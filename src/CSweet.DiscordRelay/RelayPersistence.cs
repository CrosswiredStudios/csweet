using Microsoft.EntityFrameworkCore;

namespace CSweet.DiscordRelay;

public sealed class RelayPairing
{
    public Guid Id { get; set; }
    public string OrganizationKey { get; set; } = string.Empty;
    public string AccessTokenHash { get; set; } = string.Empty;
    public string? GuildId { get; set; }
    public bool IsPaused { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RelayEnvelope
{
    public Guid Id { get; set; }
    public Guid PairingId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
}

public sealed class RelayLinkCode
{
    public Guid Id { get; set; }
    public Guid PairingId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
}

public sealed class RelayDirectRoute
{
    public Guid Id { get; set; }
    public Guid PairingId { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RelayWebhookSecret
{
    public Guid Id { get; set; }
    public Guid PairingId { get; set; }
    public string ChannelExternalId { get; set; } = string.Empty;
    public string WebhookExternalId { get; set; } = string.Empty;
    public string TokenCiphertext { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class RelayOutboundReceipt
{
    public Guid Id { get; set; }
    public Guid PairingId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ResultJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class RelayDbContext(DbContextOptions<RelayDbContext> options) : DbContext(options)
{
    public DbSet<RelayPairing> Pairings => Set<RelayPairing>();
    public DbSet<RelayEnvelope> Envelopes => Set<RelayEnvelope>();
    public DbSet<RelayLinkCode> LinkCodes => Set<RelayLinkCode>();
    public DbSet<RelayDirectRoute> DirectRoutes => Set<RelayDirectRoute>();
    public DbSet<RelayWebhookSecret> WebhookSecrets => Set<RelayWebhookSecret>();
    public DbSet<RelayOutboundReceipt> OutboundReceipts => Set<RelayOutboundReceipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RelayPairing>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrganizationKey).HasMaxLength(256).IsRequired();
            entity.Property(x => x.AccessTokenHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.GuildId).HasMaxLength(128);
            entity.HasIndex(x => x.OrganizationKey).IsUnique();
            entity.HasIndex(x => x.GuildId).IsUnique().HasFilter("\"GuildId\" IS NOT NULL");
        });
        modelBuilder.Entity<RelayEnvelope>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.PairingId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => new { x.PairingId, x.AcknowledgedAt, x.AvailableAt });
        });
        modelBuilder.Entity<RelayLinkCode>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.CodeHash).IsUnique();
        });
        modelBuilder.Entity<RelayDirectRoute>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalUserId).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.ExternalUserId).IsUnique();
        });
        modelBuilder.Entity<RelayWebhookSecret>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChannelExternalId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.WebhookExternalId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TokenCiphertext).HasMaxLength(4096).IsRequired();
            entity.HasIndex(x => new { x.PairingId, x.ChannelExternalId }).IsUnique();
        });
        modelBuilder.Entity<RelayOutboundReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ResultJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(x => new { x.PairingId, x.IdempotencyKey }).IsUnique();
        });
    }
}
