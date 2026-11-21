using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Typeshift;

// The conversations running in this container, their steering input and their watchers, by id and its type (D72)
internal sealed class LiveRuns(LiveUpdateOptions realtime, ILoggerFactory? loggers = null)
{
    private readonly ILogger _logger = LogEvents.For(loggers);
    private readonly ConcurrentDictionary<StoreId, RunClaim> _runs = new();
    private readonly ConcurrentDictionary<StoreId, LiveConversation> _live = new();
    private ImmutableList<Channel<(ConversationRun Run, bool Ended)>> _runWatchers = [];

    public IReadOnlyCollection<ConversationRun> Runs =>
        [.. _runs.Values.Select(claim => claim.Run).OfType<ConversationRun>()];

    // D50: one run per conversation at a time in a container. A run claims its conversation before it loads it, so no
    // run reads a conversation whose last run is still storing its end. A run that takes steering input is refused,
    // since the input goes to it. A claim that takes none is waited for: one still loading until its run began, and
    // a run that is ending until it ended, so the run a host starts with input that was not steered starts (D51)
    public async Task<RunClaim> ClaimAsync(StoreId conversation, CancellationToken ct)
    {
        var claim = new RunClaim(conversation);

        while (!_runs.TryAdd(conversation, claim))
        {
            if (!_runs.TryGetValue(conversation, out var held)) continue;

            var run = held.Run;

            if (run is { IsEnding: false }) throw ConversationBusyException.For(conversation);

            await (run is null ? held.Settled : held.Ended).WaitAsync(ct).ConfigureAwait(false);
        }

        return claim;
    }

    public void Begin(ConversationRun run, RunClaim? claim)
    {
        if (claim is null) return;

        claim.Begin(run);
        PublishRun(run, ended: false);
    }

    // A claim whose run never began: the run failed to begin, or a claim handed on was never taken
    public void Release(RunClaim? claim)
    {
        if (claim is not { Run: null }) return;

        _runs.TryRemove(new KeyValuePair<StoreId, RunClaim>(claim.Conversation, claim));
        claim.Settle();
    }

    // D34: a handoff ends its run and keeps the conversation, in a claim of its own for the agent handed to, so no run
    // starts in between; the run holds its claim until then, so nothing else replaced it
    public RunClaim HandOff(ConversationRun run)
    {
        var handed = new RunClaim(run.Conversation!);

        _runs.TryGetValue(run.Conversation!, out var held);
        _runs[run.Conversation!] = handed;
        held?.End();
        PublishRun(run, ended: true);

        return handed;
    }

    // A handoff replaces its run's claim before the run's own end, which then finds it gone. D54: the run's watchers
    // hear its end before a run that waits for the conversation begins
    public void End(ConversationRun run)
    {
        if (run.Conversation is not { } conversation
            || !_runs.TryGetValue(conversation, out var claim)
            || claim.Run != run)
            return;

        run.Publisher?.End(Ended(run));

        if (!_runs.TryRemove(new KeyValuePair<StoreId, RunClaim>(conversation, claim))) return;

        claim.End();
        PublishRun(run, ended: true);
    }

    // D54: a run's last update to its watchers, with the error its end stores, or the finish reason of its last model
    // call
    private static ChatResponseUpdate Ended(ConversationRun run)
    {
        var ended = run.EndError is { } error
            ? new ChatResponseUpdate(ChatRole.Assistant, [new ErrorContent(error)])
            : new ChatResponseUpdate { FinishReason = run.FinishReason };

        ended.AuthorName = run.Agent;
        ended.AdditionalProperties = new() { [ChatResponseExtensions.RunEnded] = true };
        return ended;
    }

    // D51: a claimed conversation whose run has not begun takes no steering
    public bool TrySteer(StoreId conversation, ChatMessage input) =>
        _runs.TryGetValue(conversation, out var claim)
        && claim.Run is { } run
        && run.Steer(input);

    public async IAsyncEnumerable<ChatResponseUpdate> WatchAsync(
        StoreId conversation,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var feed = CreateFeed<ChatResponseUpdate>($"conversation {conversation}");

        // D47: a watcher that joins while a run goes on reads first what the store does not hold yet, merged, and
        // then every update the run publishes after it, none twice
        var live = Enter(conversation, live =>
        {
            live.Publisher?.FlushPending();
            live.Feeds.Add(feed);

            foreach (var update in LivePublisher.Merged(live.Unstored))
            {
                feed.Writer.TryWrite(update);
            }
        });

        try
        {
            await foreach (var update in feed.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            live.Leave(left => left.Feeds.Remove(feed));
        }
    }

    // D47: a watcher or a run's publisher joins the conversation's live state under its lock; a state that is going
    // away is left to go, and a new one taken
    public LiveConversation Enter(StoreId conversation, Action<LiveConversation> join)
    {
        while (true)
        {
            var live = _live.GetOrAdd(conversation, static (id, runs) => new LiveConversation(runs, id), this);

            lock (live)
            {
                if (live.Removed) continue;

                join(live);
                return live;
            }
        }
    }

    // The last to leave, of the watchers and the run, takes the conversation's state with it
    public void Leave(StoreId conversation, LiveConversation live, Action<LiveConversation> leave)
    {
        lock (live)
        {
            leave(live);
            if (live.Feeds.Count > 0 || live.Publisher is not null) return;

            live.Removed = true;
            _live.TryRemove(KeyValuePair.Create(conversation, live));
        }
    }

    // D54: the runs going on when the watch begins, then each start and end
    public async IAsyncEnumerable<(ConversationRun Run, bool Ended)> WatchRunsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var feed = CreateFeed<(ConversationRun Run, bool Ended)>("the runs");
        ImmutableInterlocked.Update(ref _runWatchers, feeds => feeds.Add(feed));

        try
        {
            // Subscribed first, so a run starting now is in the listing or the feed; the listing's own start is skipped
            var listed = new HashSet<ConversationRun>(Runs);

            foreach (var run in listed)
            {
                yield return (run, false);
            }

            await foreach (var change in feed.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                if (!listed.Remove(change.Run) || change.Ended) yield return change;
            }
        }
        finally
        {
            ImmutableInterlocked.Update(ref _runWatchers, feeds => feeds.Remove(feed));
        }
    }

    private void PublishRun(ConversationRun run, bool ended)
    {
        foreach (var feed in _runWatchers)
        {
            feed.Writer.TryWrite((run, ended));
        }
    }

    // D47: a bounded feed per watcher that drops its oldest items, so a run never waits. The first item a watcher
    // loses is logged, since its feed then misses updates with nothing else to say so
    private Channel<T> CreateFeed<T>(string watched)
    {
        var dropped = 0;

        var options = new BoundedChannelOptions(realtime.MaxPendingUpdates)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        };

        return Channel.CreateBounded<T>(options, _ =>
        {
            if (Interlocked.Exchange(ref dropped, 1) == 0)
                LogEvents.UpdatesDropped(_logger, watched, realtime.MaxPendingUpdates);
        });
    }
}

// A conversation's place in this container, held from before its run loads it until the run ends
internal sealed class RunClaim(StoreId conversation)
{
    private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _ended = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConversationRun? _run;

    public StoreId Conversation => conversation;

    public ConversationRun? Run => Volatile.Read(ref _run);

    // Done once the run began or the claim was let go: a run waiting for the conversation then tries again
    public Task Settled => _settled.Task;

    // A full fence, so a watcher that subscribes meanwhile either lists the run or hears its start (D54)
    public void Begin(ConversationRun run)
    {
        Interlocked.Exchange(ref _run, run);
        _settled.TrySetResult();
    }

    public void Settle() => _settled.TrySetResult();

    // Done once the run ended or handed the conversation on: a run that waited for it to end then tries again
    public Task Ended => _ended.Task;

    public void End() => _ended.TrySetResult();
}

// D47: what a conversation shows its watchers, under its own lock: their feeds, the publisher of the run going on,
// and what that run published that the store does not hold yet. It lives while a watcher or a run holds it
internal sealed class LiveConversation(LiveRuns runs, StoreId conversation)
{
    public List<Channel<ChatResponseUpdate>> Feeds { get; } = [];

    public List<ChatResponseUpdate> Unstored { get; } = [];

    public LivePublisher? Publisher { get; set; }

    public bool Removed { get; set; }

    public void Leave(Action<LiveConversation> leave) => runs.Leave(conversation, this, leave);
}
