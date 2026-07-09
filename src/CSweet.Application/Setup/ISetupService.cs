using CSweet.Contracts.Setup;

namespace CSweet.Application.Setup;

public interface ISetupService
{
    Task EnsureSeededAsync(CancellationToken cancellationToken = default);
    Task<SetupStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<SetupActionResponse> CompleteStepAsync(string key, CancellationToken cancellationToken = default);
    Task<SetupActionResponse> CompleteFirstRunAsync(CancellationToken cancellationToken = default);
}
