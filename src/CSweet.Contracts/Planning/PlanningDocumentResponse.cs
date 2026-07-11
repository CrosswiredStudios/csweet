namespace CSweet.Contracts.Planning;

public sealed record PlanningDocumentResponse(
    Guid Id,
    string Title,
    string DocumentType,
    string Content,
    string? Summary,
    int Version,
    bool IsLatest,
    DateTimeOffset? GeneratedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
