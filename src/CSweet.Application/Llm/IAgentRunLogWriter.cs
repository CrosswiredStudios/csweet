using CSweet.Domain.Setup;

namespace CSweet.Application.Llm;

public interface IAgentRunLogWriter
{
    Task WriteAsync(AgentRunLog log, CancellationToken cancellationToken = default);
}
