using System.Collections.Concurrent;
using CSweet.AI.Providers;

namespace CSweet.Infrastructure.Llm;

public sealed class InMemoryLlmProviderSecretStore : ILlmProviderSecretStore
{
    private readonly ConcurrentDictionary<string, string> _secrets = new(StringComparer.Ordinal);

    public Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default)
    {
        _secrets[secretName] = secretValue;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string secretName, CancellationToken cancellationToken = default)
    {
        _secrets.TryGetValue(secretName, out var secretValue);
        return Task.FromResult(secretValue);
    }

    public Task DeleteAsync(string secretName, CancellationToken cancellationToken = default)
    {
        _secrets.TryRemove(secretName, out _);
        return Task.CompletedTask;
    }
}
