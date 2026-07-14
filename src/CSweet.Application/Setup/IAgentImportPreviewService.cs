using CSweet.Contracts.Agents;

namespace CSweet.Application.Setup;

public interface IAgentImportPreviewService
{
    Task<AgentImportPreviewResponse> PreviewAsync(
        PreviewAgentImportRequest request,
        CancellationToken cancellationToken = default);
}