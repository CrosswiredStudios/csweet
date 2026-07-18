namespace CSweet.AI.Providers;

public interface ILlmProviderSecretStore
{
    Task StoreAsync(string secretName, string secretValue, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(string secretName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string secretName, CancellationToken cancellationToken = default);
}
