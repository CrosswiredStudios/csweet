using CSweet.Application.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Infrastructure.Core;

public sealed class MemoryCaptureWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<MemoryCaptureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
                var interactiveTurnActive = await db.ChatTurns.AnyAsync(x =>
                    x.Status == ChatTurnStatus.Queued ||
                    x.Status == ChatTurnStatus.RecallingMemory ||
                    x.Status == ChatTurnStatus.Dispatching ||
                    x.Status == ChatTurnStatus.Running ||
                    x.Status == ChatTurnStatus.FinalizingMemory,
                    stoppingToken);
                if (interactiveTurnActive)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    continue;
                }
                var memory = scope.ServiceProvider.GetRequiredService<IAgentMemoryService>();
                using var enrichmentCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                var interactiveMonitor = CancelWhenInteractiveTurnStartsAsync(enrichmentCancellation, stoppingToken);
                int processed;
                try
                {
                    processed = await memory.ProcessPendingAsync(limit: 1, cancellationToken: enrichmentCancellation.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && enrichmentCancellation.IsCancellationRequested)
                {
                    logger.LogInformation("Paused memory enrichment because an interactive chat turn started.");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }
                finally
                {
                    enrichmentCancellation.Cancel();
                    await interactiveMonitor;
                }
                await Task.Delay(processed > 0 ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The memory capture worker failed a processing pass.");
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }

    private async Task CancelWhenInteractiveTurnStartsAsync(
        CancellationTokenSource enrichmentCancellation,
        CancellationToken stoppingToken)
    {
        try
        {
            while (!enrichmentCancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), enrichmentCancellation.Token);
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<CSweetDbContext>();
                var interactiveTurnActive = await db.ChatTurns.AsNoTracking().AnyAsync(x =>
                    x.Status == ChatTurnStatus.Queued ||
                    x.Status == ChatTurnStatus.RecallingMemory ||
                    x.Status == ChatTurnStatus.Dispatching ||
                    x.Status == ChatTurnStatus.Running ||
                    x.Status == ChatTurnStatus.FinalizingMemory,
                    enrichmentCancellation.Token);
                if (interactiveTurnActive)
                {
                    enrichmentCancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (enrichmentCancellation.IsCancellationRequested || stoppingToken.IsCancellationRequested)
        {
        }
    }
}
