using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.AI;

namespace Typeshift;

// The conversations running in this process, their steering input and their watchers. Ids are the build's, boxed
internal sealed class LiveRuns
{
    private readonly ConcurrentDictionary<object, RunClaim> _runs = new();
    private readonly ConcurrentDictionary<object, ImmutableList<Channel<ChatResponseUpdate>>> _watchers = new();
    private ImmutableList<Channel<(ConversationRun Run, bool Ended)>> _runWatchers = [];

    public IReadOnlyCollection<ConversationRun> Runs =>
        [.. _runs.Values.Select(claim => claim.Run).OfType<ConversationRun>()];

    // D50: one run per conversation at a time in a process. A run claims its conversation before it loads it, so no
    // run reads a conversation whose last run is still storing its end. A claim still loading is waited for, as a run
    // that began is refused, so the host's fallback for steering a run that ended keeps working (D51)
    public async Task<RunClaim?> ClaimAsync(object? conversation, CancellationToken ct)
    {
        if (conversation is null) return null;

        var claim = new RunClaim(conversation);

        while (!_runs.TryAdd(conversation, claim))
        {
            if (!_runs.TryGetValue(conversation, out var held)) continue;

            if (held.Run is not null)
            {
                throw new InvalidOperationException(
                    $"A run of conversation {conversation} is going on in this process: steer it with "
                    + "ConversationTracker.TrySteer, or wait until it ends.");
            }

            await held.Settled.WaitAsync(ct);
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

        _runs.TryRemove(new KeyValuePair<object, RunClaim>(claim.Conversation, claim));
        claim.Settle();
    }

    // D34: a handoff ends its run and keeps the conversation, in a claim of its own for the agent handed to, so no run
    // starts in between; the run holds its claim until then, so nothing else replaced it
    public RunClaim HandOff(ConversationRun run)
    {
        var handed = new RunClaim(run.Conversation!);

        _runs[run.Conversation!] = handed;
        PublishRun(run, ended: true);

        return handed;
    }

    // A handoff replaces its run's claim before the run's own end, which then finds it gone
    public void End(ConversationRun run)
    {
        if (run.Conversation is { } conversation
            && _runs.TryGetValue(conversation, out var claim)
            && claim.Run == run
            && _runs.TryRemove(new KeyValuePair<object, RunClaim>(conversation, claim)))
            PublishRun(run, ended: true);
    }

    // D51: a claimed conversation whose run has not begun takes no steering
    public bool TrySteer(object conversation, string input) =>
        _runs.TryGetValue(conversation, out var claim)
        && claim.Run is { } run
        && run.Steer(new ChatMessage(ChatRole.User, input));

    public async IAsyncEnumerable<ChatResponseUpdate> WatchAsync(
        object conversation,
        int capacity,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var feed = CreateFeed<ChatResponseUpdate>(capacity);

        _watchers.AddOrUpdate(conversation, [feed], (_, feeds) => feeds.Add(feed));

        try
        {
            await foreach (var update in feed.Reader.ReadAllAsync(ct))
            {
                yield return update;
            }
        }
        finally
        {
            // The last watcher to leave takes the conversation's entry with it
            var left = _watchers.AddOrUpdate(conversation, [], (_, feeds) => feeds.Remove(feed));

            if (left.IsEmpty) _watchers.TryRemove(KeyValuePair.Create(conversation, left));
        }
    }

    public void Publish(object conversation, ChatResponseUpdate update)
    {
        if (!_watchers.TryGetValue(conversation, out var feeds)) return;

        foreach (var feed in feeds)
        {
            feed.Writer.TryWrite(update);
        }
    }

    // D54: the runs going on when the watch begins, then each start and end
    public async IAsyncEnumerable<(ConversationRun Run, bool Ended)> WatchRunsAsync(
        int capacity,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var feed = CreateFeed<(ConversationRun Run, bool Ended)>(capacity);
        ImmutableInterlocked.Update(ref _runWatchers, feeds => feeds.Add(feed));

        try
        {
            // Subscribed first, so a run starting now is in the listing or the feed; the listing's own start is skipped
            var listed = new HashSet<ConversationRun>(Runs);

            foreach (var run in listed)
            {
                yield return (run, false);
            }

            await foreach (var change in feed.Reader.ReadAllAsync(ct))
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

    // D47: a bounded feed per watcher that drops its oldest items, so a run never waits
    private static Channel<T> CreateFeed<T>(int capacity) =>
        Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
}

// A conversation's place in this process, held from before its run loads it until the run ends
internal sealed class RunClaim(object conversation)
{
    private readonly TaskCompletionSource _settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ConversationRun? _run;

    public object Conversation => conversation;

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
}
