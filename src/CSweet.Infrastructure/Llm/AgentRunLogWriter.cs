using CSweet.Application.Llm;
using CSweet.Domain.Setup;
using CSweet.Infrastructure.Persistence;

namespace CSweet.Infrastructure.Llm;

public sealed class AgentRunLogWriter : IAgentRunLogWriter
{
    private readonly CSweetDbContext _context;

    public AgentRunLogWriter(CSweetDbContext context)
    {
        _context = context;
    }

    public async Task WriteAsync(AgentRunLog log, CancellationToken cancellationToken = default)
    {
        _context.AgentRunLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
