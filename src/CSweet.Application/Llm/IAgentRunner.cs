using CSweet.Contracts.Llm;

namespace CSweet.Application.Llm;

public interface IAgentRunner
{
    Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default);
}
