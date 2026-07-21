using System.Threading;
using CSweet.Application.Setup;

namespace CSweet.Infrastructure.Setup;

public sealed class AuditExecutionContextAccessor : IAuditExecutionContextAccessor
{
    private static readonly AsyncLocal<AuditExecutionContext?> Value = new();

    public AuditExecutionContext? Current => Value.Value;

    public IDisposable Push(AuditExecutionContext context)
    {
        var previous = Value.Value;
        Value.Value = context;
        return new RestoreScope(previous);
    }

    private sealed class RestoreScope(AuditExecutionContext? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            Value.Value = previous;
            _disposed = true;
        }
    }
}
