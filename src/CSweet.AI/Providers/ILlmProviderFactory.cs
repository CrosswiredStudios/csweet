using Microsoft.Extensions.AI;

namespace CSweet.AI.Providers;

public interface ILlmProviderFactory
{
    Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<IChatClient> CreateChatClientAsync(
        Guid providerProfileId,
        string? model,
        CancellationToken cancellationToken = default) =>
        CreateChatClientAsync(providerProfileId, cancellationToken);
}
