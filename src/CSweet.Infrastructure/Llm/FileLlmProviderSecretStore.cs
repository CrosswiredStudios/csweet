using System.Text.Json;
using CSweet.AI.Providers;

namespace CSweet.Infrastructure.Llm;

public sealed class FileLlmProviderSecretStore : ILlmProviderSecretStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly string _filePath;

    public FileLlmProviderSecretStore(string filePath)
    {
        _filePath = filePath;
    }

    public async Task StoreAsync(
        string secretName,
        string secretValue,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var secrets = await ReadSecretsAsync(cancellationToken);
            secrets[secretName] = secretValue;
            await WriteSecretsAsync(secrets, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var secrets = await ReadSecretsAsync(cancellationToken);
            return secrets.TryGetValue(secretName, out var secretValue)
                ? secretValue
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(
        string secretName,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var secrets = await ReadSecretsAsync(cancellationToken);
            if (secrets.Remove(secretName))
            {
                await WriteSecretsAsync(secrets, cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> ReadSecretsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(_filePath);
        var secrets = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(
            stream,
            SerializerOptions,
            cancellationToken);

        return secrets is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(secrets, StringComparer.Ordinal);
    }

    private async Task WriteSecretsAsync(
        Dictionary<string, string> secrets,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, secrets, SerializerOptions, cancellationToken);
    }
}
