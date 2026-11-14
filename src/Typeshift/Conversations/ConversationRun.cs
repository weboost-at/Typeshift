using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// One run, of a conversation or none: the contents it stored, by reference, since the tool loop hands the same ones on,
// even in the copies of messages it makes; the steering input and background answers waiting for the next model
// call, and where in the tool loop's messages that input went; the verdicts of its calls and the batch it asked or
// carries out (D80, D81). Without a conversation it stores nothing
internal sealed class ConversationRun(
    StoreId? conversation,
    AgentRun run,
    string model,
    StoreAccess store,
    DecisionLedger ledger,
    TimeProvider time,
    ILogger logger)
{
    private readonly Queue<ChatMessage> _steering = new();

    // Each steering input with the content it followed, so it goes back after it whatever moves ahead of it
    private readonly List<(AIContent? After, ChatMessage Message)> _injected = [];
    private readonly HashSet<AIContent> _stored = new(ReferenceEqualityComparer.Instance);

    // D59: every call id the conversation holds, the calls its history leaves out included
    private readonly HashSet<string> _callIds = [];

    // What context providers and run middleware added, which the conversation never stores
    private readonly HashSet<AIContent> _transient = new(ReferenceEqualityComparer.Instance);

    // D32: of those, the messages that stand for none of the conversation's, which compaction leaves in every call
    private readonly HashSet<AIContent> _added = new(ReferenceEqualityComparer.Instance);

    // D78: the content types the run stored its steps without, each logged once
    private readonly HashSet<Type> _unwritable = [];

    // D81: the verdict the gate gave each call that needs one, by call id; tools may run side by side
    private readonly ConcurrentDictionary<string, CallVerdict> _verdicts = [];

    // D80: the calls of the batch this run carries out whose results the conversation does not hold yet
    private readonly HashSet<string> _unsettled = [];
    private DecisionBatch? _carried;

    // D81: the calls of an answered batch, handed to the tool loop once in place of a model response
    private ChatResponse? _replay;

    // D33: the background sub-agents still working by call id, the work of each, and a signal for each answer or
    // steering input
    private readonly ConcurrentDictionary<string, string> _background = [];
    private readonly ConcurrentBag<Task> _work = [];
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly CancellationTokenSource _stopping = new();

    // D51: once the run decided to end, it takes no more steering input, which then starts a run of its own
    private bool _closed;

    // A streamed response is stored as the updates make it; the tool loop builds its own messages from the same
    // updates, so the first ones it hands on next are those, already stored
    private int _streamedUnmatched;

    public const string BackgroundCall = "typeshift.background_call";

    public StoreId? Conversation { get; } = conversation;

    public string Agent { get; } = run.Agent.Name;

    public string Model { get; } = model;

    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    public TimeProvider Time => time;

    // D80: the batch this run opened, which waits for your app. The run ends with it, unless steering input passes
    // it by first
    public DecisionBatch? Asked { get; private set; }

    // Why the store did not take the last model response, if it did not
    public Exception? ResponseFailure { get; private set; }

    public bool HasReplay => _replay is not null;

    // D47: a run that does not stream publishes each response and tool result as it stores them
    public LivePublisher? Publisher { get; set; }

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
            var working = !_background.IsEmpty;

            lock (_steering)
            {
                // D33, D80: while a batch waits, a background answer starts no model call, since the model would
                // not see the calls that wait and would make them again. The answer is stored ahead of the run's
                // end, and the model reads it once the batch is settled
                if (_steering.Any(input => Asked is null || !IsBackgroundAnswer(input))) return true;

                if (!working)
                {
                    _closed = true;
                    return false;
                }
            }

            await _arrived.WaitAsync(ct);
        }
    }

    // The input the run took and no model received, stored for the agent that answers next, after a handoff the one
    // handed to; the run takes no more
    public async Task CloseAsync(string? answering = null)
    {
        List<ChatMessage> waiting;

        lock (_steering)
        {
            _closed = true;
            waiting = [.. _steering];
            _steering.Clear();
        }

        if (waiting.Count > 0) await AppendAsync(waiting, answering: answering);
    }

    // D33: a sub-agent working while the run goes on; its answer joins the run as a message before a later model
    // call, or what went wrong, unless the run stopped
    public void StartBackground(string agent, string callId, Func<CancellationToken, Task<string>> work)
    {
        _background[callId] = agent;

        _work.Add(Task.Run(async () =>
        {
            try
            {
                var answer = await AnswerAsync(agent, work);

                if (answer is not null && !_stopping.IsCancellationRequested)
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
                _background.TryRemove(callId, out _);
                _arrived.Release();
            }
        }));
    }

    // The history and the input a run starts from are stored already
    public void MarkStored(IEnumerable<ChatMessage> messages)
    {
        var list = messages.ToList();

        foreach (var content in list.SelectMany(message => message.Contents))
        {
            _stored.Add(content);
        }

        HoldCallIds(UniqueCallIdsChatClient.Ids(list));
    }

    public void HoldCallIds(IEnumerable<string> ids)
    {
        lock (_callIds)
        {
            _callIds.UnionWith(ids);
        }
    }

    public void AddCallIdsTo(HashSet<string> used)
    {
        lock (_callIds)
        {
            used.UnionWith(_callIds);
        }
    }

    public void MarkTransient(IEnumerable<ChatMessage> messages, bool added = false)
    {
        var contents = messages.SelectMany(message => message.Contents).Where(content => !_stored.Contains(content));

        foreach (var content in contents)
        {
            _transient.Add(content);
            if (added) _added.Add(content);
        }
    }

    public bool IsTransient(ChatMessage message) => message.Contents.Any(_transient.Contains);

    public bool IsAdded(ChatMessage message) => message.Contents.Any(_added.Contains);

    public void Decide(string callId, CallVerdict verdict) => _verdicts[callId] = verdict;

    public CallVerdict? VerdictOf(string callId) => _verdicts.TryGetValue(callId, out var verdict) ? verdict : null;

    // T1: the batch exists once this returns, or the run fails and no decision does. Like every step, it is stored
    // whatever became of the run's token
    public async Task OpenAsync(DecisionBatch batch)
    {
        await ledger.OpenAsync(Conversation!, batch, CancellationToken.None);
        Asked = batch;
    }

    // The batch the run asked is expired or answered: it waits for nobody any more
    public void Settle() => Asked = null;

    // T3 happened: each call of the batch has its verdict from the record, and the calls go to the tool loop once, as
    // the response they came in. The conversation holds them already, so they are stored a single time
    public void Carry(DecisionBatch started)
    {
        _carried = started;
        _unsettled.UnionWith(started.Calls.Select(call => call.CallId));

        foreach (var call in started.Calls)
        {
            _verdicts[call.CallId] = DecisionResults.VerdictOf(call);
        }

        var calls = new ChatMessage(ChatRole.Assistant, [.. started.Calls.Select(ToContent)]);

        MarkStored([calls]);
        _replay = new ChatResponse(calls);
    }

    public ChatResponse? TakeReplay() => Interlocked.Exchange(ref _replay, null);

    // T4, T7 and the end of an expiry: the batch closes once the conversation holds a result for each of its calls.
    // A close that fails leaves the batch to the conversation's next run, which finds the results and only closes
    public async Task CloseAsync(DecisionBatch batch)
    {
        try
        {
            await ledger.CloseAsync(Conversation!, batch, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversation {Conversation} could not close its decisions.", Conversation);
            run.Activity?.AddException(exception);
        }
    }

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

        // D33: a stopped sub-agent finishes before its run ends, so none works on in a scope disposed after the run
        await Task.WhenAll(_work);
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

    // D32: what compaction left of the history, which later runs start from without reading what came before
    public Task AppendSummaryAsync(IReadOnlyList<ChatMessage> messages) => AppendAsync(messages, summary: true);

    public async Task AppendResponseAsync(ChatResponse response, ChatOptions? options, bool streamed)
    {
        // D32: with what compaction estimated the call at, the provider's report calibrates the next run
        ResponseFailure = await AppendAsync(
            [.. response.Messages],
            response.ModelId ?? options?.ModelId,
            response.Usage,
            run.Compaction?.SentEstimate);

        if (streamed) _streamedUnmatched = response.Messages.Count;
    }

    // D46: storing never observes the run's token, and a store that fails is logged while the run goes on. The
    // store's exception when it did not take the step, for the steps a decision depends on (D80)
    public async Task<Exception?> AppendAsync(
        IReadOnlyList<ChatMessage> messages,
        string? model = null,
        UsageDetails? usage = null,
        long? estimated = null,
        bool endsRun = false,
        bool summary = false,
        string? answering = null)
    {
        foreach (var message in messages)
        {
            if (message.Role != ChatRole.User) message.AuthorName ??= Agent;
        }

        MarkStored(messages);

        if (Conversation is null) return null;

        if (!endsRun && !summary) Publish(messages);

        var entry = new ConversationEntry(answering ?? Agent, Writable(messages), time.GetUtcNow())
        {
            Model = model,
            Usage = usage,
            EstimatedInputTokens = estimated,
            EndsRun = endsRun,
            Mode = endsRun || summary ? run.Mode : null,
            IsSummary = summary,
            BackgroundCalls = summary ? new Dictionary<string, string>(_background) : null
        };

        try
        {
            await store.AppendConversationAsync(Conversation, entry, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversation {Conversation} could not store a step.", Conversation);
            run.Activity?.AddException(exception);

            return exception;
        }

        await SettleCarriedAsync(messages);
        return null;
    }

    // T4: the append that holds the last result of the batch the run carries out closes it
    private async Task SettleCarriedAsync(IReadOnlyList<ChatMessage> stored)
    {
        if (_carried is not { } carried) return;

        _unsettled.ExceptWith(stored
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Select(result => result.CallId));

        if (_unsettled.Count > 0) return;

        _carried = null;
        await CloseAsync(carried);
    }

    // The arguments a provider's client delivers are JSON values, and so are the stored ones
    private static AIContent ToContent(BatchCall call) =>
        new FunctionCallContent(
            call.CallId,
            call.Tool,
            call.Arguments.EnumerateObject().ToDictionary(pair => pair.Name, object? (pair) => pair.Value));

    private static bool IsBackgroundAnswer(ChatMessage input) =>
        input.AdditionalProperties?.ContainsKey(BackgroundCall) is true;

    // D78: a content Microsoft.Extensions.AI's contract cannot write would fail the append of its whole step, so the
    // step is stored without it
    private IReadOnlyList<ChatMessage> Writable(IReadOnlyList<ChatMessage> messages)
    {
        var left = messages
            .SelectMany(message => message.Contents)
            .Where(content => !ConversationEntry.CanWrite(content))
            .ToList();

        if (left.Count == 0) return messages;

        foreach (var type in left.Select(content => content.GetType()).Where(_unwritable.Add))
        {
            logger.LogWarning(
                "Conversation {Conversation} is stored without the {Content} contents of its steps: "
                + "Microsoft.Extensions.AI's content contract cannot write that type.",
                Conversation,
                type.FullName);
        }

        return [.. messages.Select(Writable).OfType<ChatMessage>()];
    }

    private static ChatMessage? Writable(ChatMessage message)
    {
        if (message.Contents.All(ConversationEntry.CanWrite)) return message;

        var step = message.Clone();
        step.Contents = [.. message.Contents.Where(ConversationEntry.CanWrite)];
        return step.Contents.Count > 0 ? step : null;
    }

    private void Publish(IEnumerable<ChatMessage> messages)
    {
        if (Publisher is null) return;

        foreach (var message in messages.Where(message => message.Role != ChatRole.User))
        {
            var update = new ChatResponseUpdate(message.Role, message.Contents)
            {
                AuthorName = message.AuthorName,
                MessageId = message.MessageId
            };

            Publisher.Add(update);
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

    // Null once the run stopped: it cancelled the sub-agent, nothing hears its answer, and the task must not fault
    private async Task<string?> AnswerAsync(string agent, Func<CancellationToken, Task<string>> work)
    {
        try
        {
            return $"The background agent {agent} answered:\n\n{await work(_stopping.Token)}";
        }
        catch (ToolException exception) when (!_stopping.IsCancellationRequested)
        {
            return $"The background agent {agent} failed: {exception.Message}";
        }
        catch (Exception exception) when (!_stopping.IsCancellationRequested)
        {
            // D53: a message nobody wrote for the model stays with the app, as a failed tool call's does
            logger.LogError(exception, "The background agent {Agent} failed in {Conversation}.", agent, Conversation);
            run.Activity?.AddException(exception);

            return $"The background agent {agent} failed. {ToolLoop.Undisclosed}";
        }
        catch (Exception)
        {
            return null;
        }
    }
}
