using CSweet.Communications.Abstractions;
using CSweet.Contracts.Communications;

namespace CSweet.Application.Communications;

public interface ICommunicationIngressHandler
{
    Task<CommunicationActionResponse> IngestAsync(
        Guid pluginInstallationId,
        Guid organizationId,
        NormalizedCommunicationEnvelope envelope,
        CancellationToken cancellationToken = default);
}
