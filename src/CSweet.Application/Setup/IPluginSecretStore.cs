namespace CSweet.Application.Setup;

public interface IPluginSecretStore
{
    Task SetAsync(Guid installationId, string key, string value, CancellationToken cancellationToken = default);
    Task<string?> GetAsync(Guid installationId, string key, CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid installationId, string key, CancellationToken cancellationToken = default);
}
