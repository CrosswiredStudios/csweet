using CSweet.Contracts.Llm;

namespace CSweet.Application.Llm;

public interface IAgentWorkflowRunner
{
    Task<AgentWorkflowRunResult> RunAsync(
        AgentWorkflowRunRequest request,
        CancellationToken cancellationToken = default);
}
