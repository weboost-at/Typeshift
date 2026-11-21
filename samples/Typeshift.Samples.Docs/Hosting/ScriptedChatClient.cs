using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: scripted-chat-client
// A model for tests: it answers each call with the next message of its script, and keeps what each call was sent
internal sealed class ScriptedChatClient(params ChatMessage[] script) : IChatClient
{
    private readonly Queue<ChatMessage> _script = new(script);

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(([.. messages], options));
        return Task.FromResult(new ChatResponse(_script.Dequeue()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
// end-snippet
