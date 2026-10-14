using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// One run, of a conversation or none: the contents it stored, by reference, since the tool loop hands the same ones on,
// even in the copies of messages it makes; the steering input and background answers waiting for the next model
// call, and where in the tool loop's messages that input went. Without a conversation it stores nothing
internal sealed class ConversationRun(
    object? conversation,
    AgentRun run,
    string model,
    IStoreAccess store,
    TimeProvider time,
    ILogger logger)
{
    private readonly Queue<ChatMessage> _steering = new();

    // Each steering input with the content it followed, so it goes back after it whatever moves ahead of it
    private readonly List<(AIContent? After, ChatMessage Message)> _injected = [];
    private readonly HashSet<AIContent> _stored = new(ReferenceEqualityComparer.Instance);

    // What context providers and run middleware added, which the conversation never stores
    private readonly HashSet<AIContent> _transient = new(ReferenceEqualityComparer.Instance);

    // D33: the background sub-agents still working, and a signal for each answer or steering input
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly CancellationTokenSource _stopping = new();
    private int _background;

    // D51: once the run decided to end, it takes no more steering input, which then starts a run of its own
    private bool _closed;

    // A streamed response is stored as the updates make it; the tool loop builds its own messages from the same
    // updates, so the first ones it hands on next are those, already stored
    private int _streamedUnmatched;

    public const string BackgroundCall = "typeshift.background_call";

    public object? Conversation { get; } = conversation;

    public string Agent { get; } = run.Agent.Name;

    public string Model { get; } = model;

    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    public bool IsSteered
    {
        get
        {
            lock (_steering)
            {
                return _steering.Count > 0;
            }
        }
    }

    // False once the run is ending: the input then reaches no model and the host starts a run with it instead
    public bool Steer(ChatMessage input)
    {
        lock (_steering)
        {
            if (_closed) return false;
            _steering.Enqueue(input);
        }

        _arrived.Release();
        return true;
    }

    // D51, D33: whether the run goes on: steering input waits, or a background sub-agent still works, whose answer
    // the run waits for. A run that does not go on takes no more input
    public async Task<bool> GoesOnAsync(CancellationToken ct)
    {
        while (true)
        {
            // Read before the queue: a sub-agent queues its answer before it counts itself done
            var working = Volatile.Read(ref _background);

            lock (_steering)
            {
                if (_steering.Count > 0) return true;

                if (working == 0)
                {
                    _closed = true;
                    return false;
                }
            }

            await _arrived.WaitAsync(ct);
        }
    }

    // The input the run took and no model received, stored as the conversation's; the run takes no more
    public async Task CloseAsync()
    {
        List<ChatMessage> waiting;

        lock (_steering)
        {
            _closed = true;
            waiting = [.. _steering];
            _steering.Clear();
        }

        if (waiting.Count > 0) await AppendAsync(waiting);
    }

    // D33: a sub-agent working while the run goes on; its answer joins the run as a message before a later model
    // call, or what went wrong, unless the run stopped
    public void StartBackground(string agent, string callId, Func<CancellationToken, Task<ChatResponse>> work)
    {
        Interlocked.Increment(ref _background);

        _ = Task.Run(async () =>
        {
            try
            {
                var answer = await AnswerAsync(agent, work);

                if (!_stopping.IsCancellationRequested)
                {
                    var message = new ChatMessage(ChatRole.User, answer)
                    {
                        AdditionalProperties = new() { [BackgroundCall] = callId }
                    };

                    // A run already ending stops its sub-agents, the next run hears they were lost
                    _ = Steer(message);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _background);
                _arrived.Release();
            }
        });
    }

    // The history and the input a run starts from are stored already
    public void MarkStored(IEnumerable<ChatMessage> messages)
    {
        foreach (var content in messages.SelectMany(message => message.Contents))
        {
            _stored.Add(content);
        }
    }

    public void MarkTransient(IEnumerable<ChatMessage> messages)
    {
        var added = messages.SelectMany(message => message.Contents).Where(content => !_stored.Contains(content));

        foreach (var content in added)
        {
            _transient.Add(content);
        }
    }

    public bool IsTransient(ChatMessage message) => message.Contents.Any(_transient.Contains);

    // The tool loop's list with the steering input put back where it went in; the steps not stored yet (tool results)
    // are stored, then the steering input waiting is taken in and stored (D51)
    public async Task<List<ChatMessage>> PrepareCallAsync(IEnumerable<ChatMessage> messages)
    {
        var call = messages.ToList();

        foreach (var (after, message) in _injected)
        {
            var at = call.FindIndex(step => step.Contents.Any(content => ReferenceEquals(content, after)));
            call.Insert(at < 0 ? call.Count : at + 1, message);
        }

        // The tool loop's own copies of the streamed response come first, never a tool result
        var steps = call.Select(Unstored).OfType<ChatMessage>().ToList();
        var streamed = steps.Take(_streamedUnmatched).TakeWhile(step => step.Role == ChatRole.Assistant).Count();

        MarkStored(steps.Take(streamed));
        _streamedUnmatched = 0;

        if (steps.Count > streamed) await AppendAsync([.. steps.Skip(streamed)]);

        var steering = new List<ChatMessage>();

        lock (_steering)
        {
            while (_steering.TryDequeue(out var input))
            {
                _injected.Add((call.LastOrDefault()?.Contents.LastOrDefault(), input));
                call.Add(input);
                steering.Add(input);
            }
        }

        if (steering.Count > 0) await AppendAsync(steering);
        return call;
    }

    // D46: the end of a run is an entry of its own, with what went wrong and the mode it ended in (D52); background
    // sub-agents still working stop, and input no model received is stored ahead of it
    public async Task EndAsync(string? error)
    {
        _stopping.Cancel();
        await CloseAsync();

        await AppendAsync(
            error is null ? [] : [new ChatMessage(ChatRole.Assistant, [new ErrorContent(error)])],
            endsRun: true);
    }

    // The tool results of a tool loop that made no model call after them, a loop a tool or handoff ended
    public async Task StoreResultsAsync(IEnumerable<ChatMessage> messages)
    {
        var results = messages.Where(message => message.Role == ChatRole.Tool).Select(Unstored).OfType<ChatMessage>();
        if (results.ToList() is { Count: > 0 } steps) await AppendAsync(steps);
    }

    // D34: the result of a handoff call names the agent handed to, which answers from here on
    public Task AppendHandoffAsync(ChatMessage result, string target) => AppendAsync([result], answering: target);

    // D32: what compaction left of the history, which later runs start from
    public Task AppendSummaryAsync(IReadOnlyList<ChatMessage> messages) => AppendAsync(messages, summary: true);

    public async Task AppendResponseAsync(ChatResponse response, ChatOptions? options, bool streamed)
    {
        await AppendAsync([.. response.Messages], response.ModelId ?? options?.ModelId, response.Usage);
        if (streamed) _streamedUnmatched = response.Messages.Count;
    }

    // D46: storing never observes the run's token, and a store that fails is logged while the run goes on
    public async Task AppendAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model = null,
        UsageDetails? usage = null,
        bool endsRun = false,
        bool summary = false,
        string? answering = null)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User) message.AuthorName ??= Agent;
        }

        MarkStored(messages);
        if (Conversation is null) return;

        var entry = new ConversationEntry(answering ?? Agent, messages, time.GetUtcNow())
        {
            Model = model,
            Usage = usage,
            EndsRun = endsRun,
            Mode = endsRun ? run.Mode : null,
            IsSummary = summary
        };

        try
        {
            await store.AppendConversationAsync(Conversation, entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversation {Conversation} could not store a step.", Conversation);
            run.Activity?.AddException(exception);
        }
    }

    // The contents of a message not stored yet, as a message of their own; null when all are
    private ChatMessage? Unstored(ChatMessage message)
    {
        if (message.Contents.All(IsKnown)) return null;
        if (!message.Contents.Any(IsKnown)) return message;

        var step = message.Clone();
        step.Contents = [.. message.Contents.Where(content => !IsKnown(content))];
        return step;
    }

    private bool IsKnown(AIContent content) => _stored.Contains(content) || _transient.Contains(content);

    private async Task<string> AnswerAsync(string agent, Func<CancellationToken, Task<ChatResponse>> work)
    {
        try
        {
            return $"The background agent {agent} answered:\n\n{(await work(_stopping.Token)).Text}";
        }
        catch (Exception exception) when (!_stopping.IsCancellationRequested)
        {
            return $"The background agent {agent} failed: {exception.Message}";
        }
    }
}
