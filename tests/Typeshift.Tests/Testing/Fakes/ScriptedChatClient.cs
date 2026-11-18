using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Testing.Fakes;

// One Reply scripts one inner call: updates are yielded, a TaskCompletionSource awaited, an exception thrown
public sealed class ScriptedChatClient(CancellationToken limit) : IChatClient
{
    private readonly Queue<object[]> _replies = new();
    private readonly List<(int Count, TaskCompletionSource Reached)> _waiting = [];
    private readonly List<string> _unscripted = [];
    private int _scripted;

    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public bool Disposed { get; private set; }

    public Task Requested => RequestedAsync(1);

    // The replies no request took, each as the test wrote it
    public IReadOnlyList<string> Unused
    {
        get
        {
            lock (_waiting)
            {
                return [.. _replies.Select(DescribeReply)];
            }
        }
    }

    // The last message of each request that came after the script ran out, also where the run caught the failure
    public IReadOnlyList<string> Unscripted
    {
        get
        {
            lock (_waiting)
            {
                return [.. _unscripted];
            }
        }
    }

    // Completes once the client received that many requests, and is cancelled with the test's time limit
    public Task RequestedAsync(int count)
    {
        lock (_waiting)
        {
            if (Requests.Count >= count) return Task.CompletedTask;

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiting.Add((count, reached));
            return reached.Task.WaitAsync(limit);
        }
    }

    public ScriptedChatClient Reply(params object[] steps)
    {
        lock (_waiting)
        {
            _replies.Enqueue(steps);
            _scripted++;
        }

        return this;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var step in Take([.. messages], options))
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

    private object[] Take(List<ChatMessage> messages, ChatOptions? options)
    {
        lock (_waiting)
        {
            Requests.Add(messages);
            Options.Add(options);

            foreach (var waiting in _waiting.Where(waiting => waiting.Count <= Requests.Count).ToList())
            {
                waiting.Reached.TrySetResult();
                _waiting.Remove(waiting);
            }

            if (_replies.TryDequeue(out var steps)) return steps;

            var last = messages.Count == 0 ? "no message" : DescribeMessage(messages[^1]);
            _unscripted.Add(last);

            throw new InvalidOperationException(
                $"The test scripted {Replies(_scripted)}; the run asked for one more with: {last}");
        }
    }

    private static string Replies(int count) => count == 1 ? "1 reply" : $"{count} replies";

    private static string DescribeReply(object[] steps) => string.Join(", ", steps.Select(DescribeStep));

    private static string DescribeStep(object step) =>
        step switch
        {
            ChatResponseUpdate update => string.Join(", ", update.Contents.Select(DescribeContent)),
            TaskCompletionSource => "a gate",
            Exception failure => $"{failure.GetType().Name} '{failure.Message}'",
            _ => $"{step}"
        };

    private static string DescribeMessage(ChatMessage message) =>
        $"{message.Role}: {string.Join(", ", message.Contents.Select(DescribeContent))}";

    private static string DescribeContent(AIContent content) =>
        content switch
        {
            TextContent text => $"'{text.Text}'",
            FunctionCallContent call => $"a call of {call.Name} ({call.CallId})",
            FunctionResultContent result => $"the result of {result.CallId} '{result.Result}'",
            _ => content.GetType().Name
        };
}
