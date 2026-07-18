using CSweet.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace CSweet.Api.Notifications;

public sealed class SignalRApplicationRealtimePublisher(IHubContext<AppEventsHub> hub) : IApplicationRealtimePublisher
{
    public Task PublishAsync(ApplicationRealtimePublication publication, CancellationToken cancellationToken = default)
    {
        if (publication.RecipientOrganizationUserIds.Count == 0) return Task.CompletedTask;
        var groups = publication.RecipientOrganizationUserIds.Distinct()
            .Select(AppEventGroups.OrganizationUser).ToList();
        return hub.Clients.Groups(groups).SendAsync("AppEvent", publication.Envelope, cancellationToken);
    }
}

public sealed class ApplicationRealtimeOutboxWorker(
    IServiceProvider services,
    ILogger<ApplicationRealtimeOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IApplicationRealtimeOutboxDispatcher>();
                var publisher = scope.ServiceProvider.GetRequiredService<IApplicationRealtimePublisher>();
                var count = await dispatcher.DispatchBatchAsync(publisher, cancellationToken: stoppingToken);
                await Task.Delay(count > 0 ? TimeSpan.FromMilliseconds(100) : TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Application realtime dispatch failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
