namespace CSweet.Contracts.Planning;

public sealed record GeneratePlanningDocumentRequest(
    Guid OrganizationId,
    string DocumentType,
    Guid ProviderProfileId);
