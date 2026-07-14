using System.Text.Json;

namespace CSweet.Contracts.Agents;

public sealed record AgentCatalogItemResponse(
    string Id,
    string Name,
    string Version,
    string PublisherId,
    string PublisherName,
    string RuntimeType,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> RequestedPermissions);

public sealed record AgentConfigurationSchemaResponse(
    string AgentId,
    string AgentVersion,
    string SchemaVersion,
    IReadOnlyList<AgentConfigurationField> Fields,
    IReadOnlyDictionary<string, JsonElement> Settings);

public sealed record AgentConfigurationField(
    string Key,
    string Label,
    string Type,
    bool Required,
    string? Description = null,
    string? Placeholder = null,
    IReadOnlyList<AgentConfigurationOption>? Options = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    decimal? Step = null,
    string? DependsOnFieldKey = null);

public sealed record AgentConfigurationOption(
    string Value,
    string Label);

public sealed record UpdateAgentConfigurationRequest(
    IReadOnlyDictionary<string, JsonElement> Settings)
{
    public string? SchemaVersion { get; init; }
}

public sealed record AgentConfigurationUpdateResponse(
    bool Succeeded,
    string? Message,
    IReadOnlyDictionary<string, JsonElement> Settings);

public static class AgentConfigurationCapabilities
{
    public const string Describe = "agent.configuration.describe.v1";

    public const string Update = "agent.configuration.update.v1";
}

public static class AgentConfigurationFieldTypes
{
    public const string Text = "text";

    public const string TextArea = "textarea";

    public const string Number = "number";

    public const string Boolean = "boolean";

    public const string Select = "select";

    public const string Secret = "secret";

    public const string LlmProvider = "llmProvider";

    public const string LlmModel = "llmModel";
}
