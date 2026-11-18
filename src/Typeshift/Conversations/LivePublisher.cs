using Microsoft.Extensions.AI;

namespace Typeshift;

// D47: text, reasoning and usage merge until the interval passes, the message they belong to changes or the model's
// stream ends; anything else goes out at once, after them. What the run published that the store does not hold yet
// is kept, merged for a watcher that joins while the run goes on, and the run's end goes out last (D54). D74: with
// nobody watching, an update costs a lock nobody else holds and a place in a list the store's next step empties
internal sealed class LivePublisher
{
    private readonly LiveConversation _live;
    private readonly LiveUpdateOptions _options;
    private readonly TimeProvider _time;
    private ChatResponseUpdate? _pending;
    private DateTimeOffset _published = DateTimeOffset.MinValue;

    public LivePublisher(LiveRuns runs, StoreId conversation, LiveUpdateOptions options, TimeProvider time)
    {
        _options = options;
        _time = time;
        _live = runs.Enter(conversation, live => live.Publisher = this);
    }

    public void Add(ChatResponseUpdate update)
    {
        lock (_live)
        {
            _live.Unstored.Add(update);

            // A watcher that comes later reads what the store does not hold, so nothing merged for none is kept
            if (_live.Feeds.Count == 0)
            {
                _pending = null;
                return;
            }

            if (!IsMerged(update))
            {
                FlushPending();
                Publish(update);
                return;
            }

            if (_pending is not null && StartsAnother(_pending, update)) FlushPending();

            _pending = Merge(_pending, update);

            if (_time.GetUtcNow() - _published >= _options.PublishInterval) FlushPending();
        }
    }

    public void Flush()
    {
        lock (_live)
        {
            FlushPending();
        }
    }

    // D47: the store took the run's steps up to here, so a watcher that joins later reads them there
    public void Stored()
    {
        lock (_live)
        {
            FlushPending();
            _live.Unstored.Clear();
        }
    }

    // D54: the run's last update, after the rest; the conversation's live state goes with the last watcher, and a
    // run that began meanwhile keeps its own publisher
    public void End(ChatResponseUpdate ended) =>
        _live.Leave(live =>
        {
            FlushPending();
            Publish(ended);
            live.Unstored.Clear();

            if (live.Publisher == this) live.Publisher = null;
        });

    // Under the lock, as a watcher joins too: the text that waits goes to the watchers there are, and one that joins
    // reads it among what the store does not hold
    public void FlushPending()
    {
        if (_pending is null) return;

        Publish(_pending);
        _pending = null;
    }

    // What the store does not hold yet, merged as the run published it, for a watcher that joins now
    public static List<ChatResponseUpdate> Merged(IReadOnlyList<ChatResponseUpdate> updates)
    {
        var merged = new List<ChatResponseUpdate>();
        ChatResponseUpdate? text = null;

        foreach (var update in updates)
        {
            if (text is not null && (!IsMerged(update) || StartsAnother(text, update)))
            {
                merged.Add(text);
                text = null;
            }

            if (IsMerged(update)) text = Merge(text, update);
            else merged.Add(update);
        }

        if (text is not null) merged.Add(text);
        return merged;
    }

    private static bool IsMerged(ChatResponseUpdate update) =>
        update.Contents.All(content => content is TextContent or TextReasoningContent or UsageContent);

    // A copy with the first update's ids and a list of its own, so a watcher groups the merged text as the run's
    // caller does and the merge never copies what it holds; the end of the model's answer keeps its finish reason
    private static ChatResponseUpdate Merge(ChatResponseUpdate? merged, ChatResponseUpdate update)
    {
        if (merged is null)
        {
            merged = update.Clone();
            merged.Contents = [.. update.Contents];
            return merged;
        }

        foreach (var content in update.Contents)
        {
            merged.Contents.Add(content);
        }

        merged.FinishReason = update.FinishReason ?? merged.FinishReason;
        return merged;
    }

    // An update of another message, response, role or author than the text that waits, as the caller's
    // ToChatResponse tells them apart: an id the update leaves out belongs to the message going on
    private static bool StartsAnother(ChatResponseUpdate pending, ChatResponseUpdate update) =>
        Differs(update.MessageId, pending.MessageId)
        || Differs(update.ResponseId, pending.ResponseId)
        || Differs(update.AuthorName, pending.AuthorName)
        || (update.Role is { } role && pending.Role is { } waiting && role != waiting);

    private static bool Differs(string? id, string? pending) =>
        id is { Length: > 0 } && pending is { Length: > 0 } && id != pending;

    private void Publish(ChatResponseUpdate update)
    {
        foreach (var feed in _live.Feeds)
        {
            feed.Writer.TryWrite(update);
        }

        _published = _time.GetUtcNow();
    }
}
