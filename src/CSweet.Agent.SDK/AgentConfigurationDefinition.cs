using System.Text.Json;
using CSweet.Contracts.Agents;

namespace CSweet.Agent.SDK;

public sealed class AgentConfigurationDefinition
{
    internal AgentConfigurationDefinition(
        IReadOnlyList<AgentConfigurationField> fields,
        IReadOnlyDictionary<string, JsonElement> defaultSettings)
    {
        Fields = fields;
        DefaultSettings = defaultSettings;
    }

    public IReadOnlyList<AgentConfigurationField> Fields { get; }

    public IReadOnlyDictionary<string, JsonElement> DefaultSettings { get; }
}

public sealed class AgentConfigurationBuilder
{
    private readonly List<AgentConfigurationField> _fields = [];
    private readonly Dictionary<string, JsonElement> _defaultSettings = new(StringComparer.Ordinal);

    public AgentConfigurationBuilder LlmProvider(
        string key,
        string label,
        bool required = false,
        string? description = null,
        string defaultValue = "") =>
        AddTextLikeField(
            key,
            label,
            AgentConfigurationFieldTypes.LlmProvider,
            required,
            description,
            placeholder: null,
            defaultValue);

    public AgentConfigurationBuilder LlmModel(
        string key,
        string label,
        string dependsOnFieldKey,
        bool required = false,
        string? description = null,
        string defaultValue = "") =>
        AddField(
            new AgentConfigurationField(
                key,
                label,
                AgentConfigurationFieldTypes.LlmModel,
                required,
                description,
                DependsOnFieldKey: dependsOnFieldKey),
            defaultValue);

    public AgentConfigurationBuilder Select(
        string key,
        string label,
        IEnumerable<AgentConfigurationOption> options,
        bool required = false,
        string? description = null,
        string? defaultValue = null) =>
        AddField(
            new AgentConfigurationField(
                key,
                label,
                AgentConfigurationFieldTypes.Select,
                required,
                description,
                Options: options.ToList()),
            defaultValue ?? options.FirstOrDefault()?.Value ?? string.Empty);

    public AgentConfigurationBuilder Boolean(
        string key,
        string label,
        bool required = false,
        string? description = null,
        bool defaultValue = false) =>
        AddField(
            new AgentConfigurationField(
                key,
                label,
                AgentConfigurationFieldTypes.Boolean,
                required,
                description),
            defaultValue);

    public AgentConfigurationBuilder Number(
        string key,
        string label,
        bool required = false,
        string? description = null,
        decimal? minimum = null,
        decimal? maximum = null,
        decimal? step = null,
        decimal? defaultValue = null) =>
        AddField(
            new AgentConfigurationField(
                key,
                label,
                AgentConfigurationFieldTypes.Number,
                required,
                description,
                Minimum: minimum,
                Maximum: maximum,
                Step: step),
            defaultValue);

    public AgentConfigurationBuilder Text(
        string key,
        string label,
        bool required = false,
        string? description = null,
        string? placeholder = null,
        string defaultValue = "") =>
        AddTextLikeField(
            key,
            label,
            AgentConfigurationFieldTypes.Text,
            required,
            description,
            placeholder,
            defaultValue);

    public AgentConfigurationBuilder TextArea(
        string key,
        string label,
        bool required = false,
        string? description = null,
        string? placeholder = null,
        string defaultValue = "") =>
        AddTextLikeField(
            key,
            label,
            AgentConfigurationFieldTypes.TextArea,
            required,
            description,
            placeholder,
            defaultValue);

    public AgentConfigurationBuilder Secret(
        string key,
        string label,
        bool required = false,
        string? description = null,
        string? placeholder = null,
        string defaultValue = "") =>
        AddTextLikeField(
            key,
            label,
            AgentConfigurationFieldTypes.Secret,
            required,
            description,
            placeholder,
            defaultValue);

    public AgentConfigurationDefinition Build()
    {
        var duplicateKey = _fields
            .GroupBy(field => field.Key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"Agent configuration field '{duplicateKey}' is defined more than once.");
        }

        return new AgentConfigurationDefinition(
            _fields.ToList(),
            CloneSettings(_defaultSettings));
    }

    private AgentConfigurationBuilder AddTextLikeField(
        string key,
        string label,
        string type,
        bool required,
        string? description,
        string? placeholder,
        string defaultValue) =>
        AddField(
            new AgentConfigurationField(
                key,
                label,
                type,
                required,
                description,
                placeholder),
            defaultValue);

    private AgentConfigurationBuilder AddField(AgentConfigurationField field, object? defaultValue)
    {
        _fields.Add(field);
        _defaultSettings[field.Key] = JsonSerializer.SerializeToElement(defaultValue, CSweetAgentBase.SerializerOptions);
        return this;
    }

    private static Dictionary<string, JsonElement> CloneSettings(IReadOnlyDictionary<string, JsonElement> settings) =>
        settings.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
}
