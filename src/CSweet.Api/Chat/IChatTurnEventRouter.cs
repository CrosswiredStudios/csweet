using System.Collections.Concurrent;
using System.Threading.Channels;
using CSweet.Contracts.Core;

namespace CSweet.Api.Chat;

public interface IChatTurnEventRouter
{
    ChannelReader<ChatTurnTraceEventResponse> Subscribe(Guid turnId);
    void Publish(ChatTurnTraceEventResponse traceEvent);
    void Complete(Guid turnId);
}

public sealed class ChatTurnEventRouter : IChatTurnEventRouter
{
    private readonly ConcurrentDictionary<Guid, Channel<ChatTurnTraceEventResponse>> _channels = new();

    public ChannelReader<ChatTurnTraceEventResponse> Subscribe(Guid turnId) =>
        _channels.GetOrAdd(turnId, _ => Channel.CreateUnbounded<ChatTurnTraceEventResponse>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = false })).Reader;

    public void Publish(ChatTurnTraceEventResponse traceEvent) =>
        _channels.GetOrAdd(traceEvent.ChatTurnId, _ => Channel.CreateUnbounded<ChatTurnTraceEventResponse>()).Writer.TryWrite(traceEvent);

    public void Complete(Guid turnId)
    {
        if (_channels.TryRemove(turnId, out var channel)) channel.Writer.TryComplete();
    }
}
