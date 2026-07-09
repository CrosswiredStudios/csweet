using Microsoft.Extensions.AI;

namespace CSweet.AI.Providers;

public sealed class FakeLlmProviderFactory : ILlmProviderFactory
{
    private readonly IChatClient _chatClient;

    public FakeLlmProviderFactory(IChatClient? chatClient = null)
    {
        _chatClient = chatClient ?? new FakeChatClient();
    }

    public Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_chatClient);
    }
}
