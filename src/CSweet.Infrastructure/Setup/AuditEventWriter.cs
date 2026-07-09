using CSweet.Application.Setup;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;

namespace CSweet.Infrastructure.Setup;

public sealed class AuditEventWriter : IAuditEventWriter
{
    private readonly CSweetDbContext _dbContext;

    public AuditEventWriter(CSweetDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string eventType,
        string entityType,
        Guid? entityId,
        string? summary,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            MetadataJson = metadataJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
