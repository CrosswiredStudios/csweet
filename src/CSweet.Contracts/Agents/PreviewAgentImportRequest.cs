namespace CSweet.Contracts.Agents;

public sealed record PreviewAgentImportRequest(
    string RepositoryUrl,
    string? Ref = null);