using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentRuntimeStartupCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AgentRuntimeStartupCleanupWorker> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<AgentRuntimeStartupCleanupService>()
                .CleanupAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Agent runtime startup cleanup failed; normal reconciliation will continue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
