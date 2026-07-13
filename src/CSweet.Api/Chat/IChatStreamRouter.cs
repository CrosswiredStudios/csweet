using System.Threading.Channels;

namespace CSweet.Api.Chat;

public sealed record ChatStreamChunk(int Sequence, string Delta, bool IsFinal, string? Error = null);

public interface IChatStreamRouter
{
    ChannelReader<ChatStreamChunk> Subscribe(Guid conversationId);

    void Publish(Guid conversationId, ChatStreamChunk chunk);

    void Complete(Guid conversationId);
}
