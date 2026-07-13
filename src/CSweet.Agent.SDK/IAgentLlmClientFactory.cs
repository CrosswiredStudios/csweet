using Microsoft.Extensions.AI;

namespace CSweet.Agent.SDK;

public sealed record AgentLlmSelection(
    Guid ProviderProfileId,
    string? Model = null);

public interface IAgentLlmClientFactory
{
    Task<IChatClient> CreateChatClientAsync(
        AgentLlmSelection selection,
        CancellationToken cancellationToken = default);
}
