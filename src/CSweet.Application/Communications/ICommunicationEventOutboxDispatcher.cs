using CSweet.Contracts.Communications;

namespace CSweet.Application.Communications;

public sealed record CommunicationEventPublication(
    string EventType,
    string Subject,
    CommunicationEventEnvelope Envelope,
    Guid TargetInstallationId);

public interface ICommunicationEventPublisher
{
    Task PublishAsync(CommunicationEventPublication publication, CancellationToken cancellationToken = default);
}

public interface ICommunicationEventOutboxDispatcher
{
    Task<int> DispatchBatchAsync(
        ICommunicationEventPublisher publisher,
        int batchSize = 100,
        CancellationToken cancellationToken = default);
}
