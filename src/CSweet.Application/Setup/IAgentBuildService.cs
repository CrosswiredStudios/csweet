namespace CSweet.Application.Setup;

public interface IAgentBuildService
{
    Task<Guid> QueueAsync(
        Guid packageVersionId,
        CancellationToken cancellationToken = default);

    Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default);
}
