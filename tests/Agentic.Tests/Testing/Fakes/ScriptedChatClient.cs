using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Agentic.Tests.Testing.Fakes;

// One Reply scripts one inner call: updates are yielded, a TaskCompletionSource awaited, an exception thrown
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<object[]> _replies = new();

    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public ScriptedChatClient Reply(params object[] steps)
    {
        _replies.Enqueue(steps);
        return this;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        Requests.Add([.. messages]);
        Options.Add(options);

        foreach (var step in _replies.Dequeue())
        {
            switch (step)
            {
                case ChatResponseUpdate update:
                    yield return update;
                    break;

                case TaskCompletionSource gate:
                    await gate.Task.WaitAsync(ct);
                    break;

                case Exception failure:
                    throw failure;
            }
        }
    }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken ct = default) =>
        GetStreamingResponseAsync(messages, options, ct).ToChatResponseAsync(ct);

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
