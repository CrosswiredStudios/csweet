using CSweet.Domain.Setup;

namespace CSweet.Contracts.Llm;

public sealed class LlmProviderPreset
{
    public string Name { get; init; } = string.Empty;
    public LlmProviderType ProviderType { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string? ApiKeyPlaceholder { get; init; }
    public bool SupportsStreaming { get; init; }
    public bool SupportsToolCalling { get; init; }
    public bool SupportsStructuredOutput { get; init; }
    public bool SupportsVision { get; init; }
}
