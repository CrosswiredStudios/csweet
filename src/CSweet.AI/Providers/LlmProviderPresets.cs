using CSweet.Contracts.Llm;
using CSweet.Domain.Setup;

namespace CSweet.AI.Providers;

public static class LlmProviderPresets
{
    public static LlmProviderPreset LmStudioLocalhost()
    {
        return new LlmProviderPreset
        {
            Name = "Local LM Studio",
            ProviderType = LlmProviderType.LmStudio,
            BaseUrl = "http://localhost:1234/v1",
            ApiKeyPlaceholder = "lm-studio",
            SupportsStreaming = true,
            SupportsToolCalling = false,
            SupportsStructuredOutput = false,
            SupportsVision = false
        };
    }
}
