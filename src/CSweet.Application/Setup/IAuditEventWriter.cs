namespace CSweet.Application.Setup;

public interface IAuditEventWriter
{
    Task WriteAsync(
        string eventType,
        string entityType,
        Guid? entityId,
        string? summary,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);
}
