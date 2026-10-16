using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.Extensions.AI;

namespace Typeshift;

// The conversations running in this process, their steering input and their watchers. Ids are the build's, boxed
internal sealed class LiveRuns
{
    private readonly ConcurrentDictionary<object, ConversationRun> _runs = new();
    private readonly ConcurrentDictionary<object, ImmutableList<Channel<ChatResponseUpdate>>> _watchers = new();
    private ImmutableList<Channel<(ConversationRun Run, bool Ended)>> _runWatchers = [];

    public IReadOnlyCollection<ConversationRun> Runs => [.. _runs.Values];

    // D50: one run per conversation at a time in a process
    public void Begin(ConversationRun run)
    {
        if (run.Conversation is null) return;

        if (!_runs.TryAdd(run.Conversation, run))
        {
            throw new InvalidOperationException(
                $"A run of conversation {run.Conversation} is going on in this process: steer it with "
                + "ConversationTracker.TrySteer, or wait until it ends.");
        }

        PublishRun(run, ended: false);
    }

    // A handoff ends its run before the run's own end does, which then finds it gone
    public void End(ConversationRun run)
    {
        if (run.Conversation is { } conversation
            && _runs.TryRemove(new KeyValuePair<object, ConversationRun>(conversation, run)))
            PublishRun(run, ended: true);
    }

    // D51
    public bool TrySteer(object conversation, string input)
    {
        return _runs.TryGetValue(conversation, out var run) && run.Steer(new ChatMessage(ChatRole.User, input));
    }

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
            var listed = new HashSet<ConversationRun>(_runs.Values);

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
