using CSweet.Contracts.Planning;

namespace CSweet.Application.Planning;

public interface IPlanningDocumentService
{
    Task<IReadOnlyList<PlanningDocumentResponse>> ListByOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<PlanningDocumentResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PlanningDocumentResponse?> GetLatestByTypeAsync(Guid organizationId, string documentType, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> GenerateAsync(GeneratePlanningDocumentRequest request, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> UpdateContentAsync(Guid id, string content, CancellationToken cancellationToken = default);
    Task<PlanningActionResponse> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
