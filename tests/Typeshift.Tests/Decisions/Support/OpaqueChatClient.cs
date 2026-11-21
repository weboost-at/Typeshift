using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Decisions.Support;

// A wrapper that hands out none of what it wraps, as a client of an app's own may, so a tool loop below it stays
// unseen by whoever asks the client for one
public sealed class OpaqueChatClient(IChatClient inner) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        inner.GetResponseAsync(messages, options, cancellationToken);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        inner.GetStreamingResponseAsync(messages, options, cancellationToken);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() => inner.Dispose();
}
