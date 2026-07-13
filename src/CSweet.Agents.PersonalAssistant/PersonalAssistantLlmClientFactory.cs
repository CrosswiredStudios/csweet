using CSweet.Agent.SDK;
using CSweet.AI.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CSweet.Agents.PersonalAssistant;

public sealed class PersonalAssistantLlmClientFactory : IAgentLlmClientFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PersonalAssistantLlmClientFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<IChatClient> CreateChatClientAsync(
        AgentLlmSelection selection,
        CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var providerFactory = scope.ServiceProvider.GetRequiredService<ILlmProviderFactory>();
        return await providerFactory.CreateChatClientAsync(selection.ProviderProfileId, cancellationToken);
    }
}
