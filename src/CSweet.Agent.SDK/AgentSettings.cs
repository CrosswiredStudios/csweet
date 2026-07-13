using System.Text.Json;

namespace CSweet.Agent.SDK;

public sealed class AgentSettings
{
    private readonly IReadOnlyDictionary<string, JsonElement> _settings;

    public AgentSettings(IReadOnlyDictionary<string, JsonElement> settings)
    {
        _settings = settings;
    }

    public string GetString(string key, string defaultValue = "")
    {
        return _settings.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? defaultValue
            : defaultValue;
    }

    public Guid? GetGuid(string key)
    {
        var value = GetString(key);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    public bool GetBoolean(string key, bool defaultValue = false)
    {
        return _settings.TryGetValue(key, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => defaultValue
            }
            : defaultValue;
    }

    public int GetInt32(string key, int defaultValue = 0)
    {
        return _settings.TryGetValue(key, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number)
                ? number
                : defaultValue;
    }

    public decimal GetDecimal(string key, decimal defaultValue = 0)
    {
        return _settings.TryGetValue(key, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number)
                ? number
                : defaultValue;
    }

    public IReadOnlyDictionary<string, JsonElement> AsDictionary() => _settings;
}
