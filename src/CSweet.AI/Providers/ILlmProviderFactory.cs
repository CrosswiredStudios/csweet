using Microsoft.Extensions.AI;

namespace CSweet.AI.Providers;

public interface ILlmProviderFactory
{
    Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);
}
