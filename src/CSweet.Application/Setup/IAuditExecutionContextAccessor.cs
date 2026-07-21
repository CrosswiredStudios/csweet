namespace CSweet.Application.Setup;

public sealed record AuditExecutionContext(
    Guid? OrganizationId,
    AuditActor Actor,
    Guid? ParentEventId = null,
    Guid? TraceId = null);

public interface IAuditExecutionContextAccessor
{
    AuditExecutionContext? Current { get; }
    IDisposable Push(AuditExecutionContext context);
}
