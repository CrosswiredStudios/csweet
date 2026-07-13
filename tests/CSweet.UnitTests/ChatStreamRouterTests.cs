using CSweet.Api.Chat;

namespace CSweet.UnitTests;

public sealed class ChatStreamRouterTests
{
    [Fact]
    public async Task Publish_DeliversChunksInOrder()
    {
        var router = new ChatStreamRouter();
        var conversationId = Guid.NewGuid();
        var reader = router.Subscribe(conversationId);

        router.Publish(conversationId, new ChatStreamChunk(0, "hel", IsFinal: false));
        router.Publish(conversationId, new ChatStreamChunk(1, "lo", IsFinal: false));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.True(await reader.WaitToReadAsync(timeout.Token));

        Assert.True(reader.TryRead(out var first));
        Assert.True(reader.TryRead(out var second));
        Assert.Equal(new ChatStreamChunk(0, "hel", IsFinal: false), first);
        Assert.Equal(new ChatStreamChunk(1, "lo", IsFinal: false), second);
    }

    [Fact]
    public async Task Publish_FinalChunkCompletesReader()
    {
        var router = new ChatStreamRouter();
        var conversationId = Guid.NewGuid();
        var reader = router.Subscribe(conversationId);

        router.Publish(conversationId, new ChatStreamChunk(0, string.Empty, IsFinal: true));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        Assert.True(await reader.WaitToReadAsync(timeout.Token));
        Assert.True(reader.TryRead(out var final));
        Assert.True(final.IsFinal);

        await reader.Completion.WaitAsync(timeout.Token);
    }

    [Fact]
    public void Publish_UnknownConversationIsNoOp()
    {
        var router = new ChatStreamRouter();

        router.Publish(Guid.NewGuid(), new ChatStreamChunk(0, "ignored", IsFinal: false));
    }

    [Fact]
    public async Task Complete_CompletesAndRemovesConversation()
    {
        var router = new ChatStreamRouter();
        var conversationId = Guid.NewGuid();
        var reader = router.Subscribe(conversationId);

        router.Complete(conversationId);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await reader.Completion.WaitAsync(timeout.Token);
    }
}
