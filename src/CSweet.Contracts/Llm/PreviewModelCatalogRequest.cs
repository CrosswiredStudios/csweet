using CSweet.Domain.Setup;

namespace CSweet.Contracts.Llm;

public sealed record PreviewModelCatalogRequest(
    LlmProviderType ProviderType,
    string BaseUrl,
    string? ApiKey);
