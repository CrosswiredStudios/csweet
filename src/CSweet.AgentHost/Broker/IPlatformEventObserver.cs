using CSweet.Agent.Contracts.Grpc;

namespace CSweet.AgentHost.Broker;

public interface IPlatformEventObserver
{
    bool CanObserve(string eventType);
    Task ObserveAsync(AgentSession session, PublishEvent publishedEvent, CancellationToken cancellationToken);
}
