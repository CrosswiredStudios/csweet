namespace CSweet.Contracts.Llm;

public sealed record PreviewModelCatalogResponse(
    bool Succeeded,
    string? ErrorCode,
    string? Message,
    IReadOnlyList<ModelDescriptor> Models);
