using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Testing.Fakes;

// One Reply scripts one inner call: updates are yielded, a TaskCompletionSource awaited, an exception thrown
public sealed class ScriptedChatClient : IChatClient
{
    private readonly Queue<object[]> _replies = new();
    private readonly List<(int Count, TaskCompletionSource Reached)> _waiting = [];

    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public bool Disposed { get; private set; }

    public Task Requested => RequestedAsync(1);

    // Completes once the client received that many requests
    public Task RequestedAsync(int count)
    {
        lock (_waiting)
        {
            if (Requests.Count >= count) return Task.CompletedTask;

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting.Add((count, reached));
            return reached.Task;
        }
    }

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
        lock (_waiting)
        {
            Requests.Add([.. messages]);
            Options.Add(options);

            foreach (var waiting in _waiting.Where(waiting => waiting.Count <= Requests.Count).ToList())
            {
                waiting.Reached.TrySetResult();
                _waiting.Remove(waiting);
            }
        }

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

    public void Dispose() => Disposed = true;
}
