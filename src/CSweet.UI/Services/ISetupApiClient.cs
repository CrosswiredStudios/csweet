using CSweet.Contracts.Setup;

namespace CSweet.UI.Services;

public interface ISetupApiClient
{
    Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<SetupActionResponse> CompleteStepAsync(string key, CancellationToken cancellationToken = default);
    Task<SetupActionResponse> CompleteSetupAsync(CancellationToken cancellationToken = default);
}
