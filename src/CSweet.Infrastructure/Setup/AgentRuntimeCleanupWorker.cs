using CSweet.Application.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CSweet.Infrastructure.Setup;

public sealed class AgentRuntimeCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<AgentRuntimeCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAgentRuntimeCleanupService>().CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "The agent runtime cleanup iteration failed."); }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
