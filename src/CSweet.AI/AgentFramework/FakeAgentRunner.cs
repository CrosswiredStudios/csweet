using CSweet.Application.Llm;
using CSweet.Contracts.Llm;

namespace CSweet.AI.AgentFramework;

public sealed class FakeAgentRunner : IAgentRunner
{
    public string? ResponseContent { get; set; } = "Fake agent response content.";
    public string? StructuredJson { get; set; }
    public bool SimulateFailure { get; set; } = false;
    public string FailureMessage { get; set; } = "Simulated agent failure.";

    public List<AgentRunRequest> ReceivedRequests { get; } = new();

    public Task<AgentRunResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ReceivedRequests.Add(request);

        if (SimulateFailure)
        {
            var failureLogs = new List<AgentRunLogEntry>
            {
                new("Info", "Starting fake agent run", DateTimeOffset.UtcNow),
                new("Error", FailureMessage, DateTimeOffset.UtcNow)
            };

            return Task.FromResult(new AgentRunResult(
                Succeeded: false,
                Content: null,
                StructuredJson: null,
                FailureMessage: FailureMessage,
                Logs: failureLogs));
        }

        var successLogs = new List<AgentRunLogEntry>
        {
            new("Info", "Starting fake agent run", DateTimeOffset.UtcNow),
            new("Info", "Fake agent completed successfully", DateTimeOffset.UtcNow)
        };

        return Task.FromResult(new AgentRunResult(
            Succeeded: true,
            Content: ResponseContent,
            StructuredJson: StructuredJson,
            FailureMessage: null,
            Logs: successLogs));
    }
}
