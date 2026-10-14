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
    }

    public void End(ConversationRun run)
    {
        if (run.Conversation is { } conversation)
            _runs.TryRemove(new KeyValuePair<object, ConversationRun>(conversation, run));
    }

    // D51
    public bool TrySteer(object conversation, string input)
    {
        return _runs.TryGetValue(conversation, out var run) && run.Steer(new ChatMessage(ChatRole.User, input));
    }

    // D47: a bounded feed per watcher that drops its oldest updates, so a run never waits
    public async IAsyncEnumerable<ChatResponseUpdate> WatchAsync(
        object conversation,
        int capacity,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var feed = Channel.CreateBounded<ChatResponseUpdate>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

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
}
