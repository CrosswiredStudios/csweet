using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CSweet.Api.Chat;

public sealed class ChatStreamRouter : IChatStreamRouter
{
    private readonly ConcurrentDictionary<Guid, Channel<ChatStreamChunk>> _channels = new();

    public ChannelReader<ChatStreamChunk> Subscribe(Guid conversationId)
    {
        var channel = _channels.GetOrAdd(conversationId, _ =>
            Channel.CreateBounded<ChatStreamChunk>(new BoundedChannelOptions(256)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            }));

        return channel.Reader;
    }

    public void Publish(Guid conversationId, ChatStreamChunk chunk)
    {
        if (!_channels.TryGetValue(conversationId, out var channel))
        {
            return;
        }

        channel.Writer.TryWrite(chunk);

        if (chunk.IsFinal)
        {
            channel.Writer.TryComplete();
        }
    }

    public void Complete(Guid conversationId)
    {
        if (_channels.TryRemove(conversationId, out var channel))
        {
            channel.Writer.TryComplete();
        }
    }
}
