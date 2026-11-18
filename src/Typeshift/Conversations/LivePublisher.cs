using Microsoft.Extensions.AI;

namespace Typeshift;

// D47: text, reasoning and usage merge until the interval passes, the message they belong to changes or the model's
// stream ends; anything else goes out at once, after them. D74: with nobody watching, an update costs one lookup
internal sealed class LivePublisher(
    LiveRuns runs,
    StoreId conversation,
    LiveUpdateOptions options,
    TimeProvider time)
{
    private ChatResponseUpdate? _pending;
    private DateTimeOffset _published = DateTimeOffset.MinValue;

    public void Add(ChatResponseUpdate update)
    {
        // A watcher that comes later reads from its own start, so nothing merged for none is kept
        if (!runs.IsWatched(conversation))
        {
            _pending = null;
            return;
        }

        if (!update.Contents.All(content => content is TextContent or TextReasoningContent or UsageContent))
        {
            Flush();
            Publish(update);
            return;
        }

        if (_pending is not null && StartsAnother(update)) Flush();

        if (_pending is null)
        {
            // A copy with the first update's ids and a list of its own, so a watcher groups the merged text as the
            // run's caller does and the merge never copies what it holds
            _pending = update.Clone();
            _pending.Contents = [.. update.Contents];
        }
        else
        {
            foreach (var content in update.Contents)
            {
                _pending.Contents.Add(content);
            }
        }

        if (time.GetUtcNow() - _published >= options.PublishInterval) Flush();
    }

    public void Flush()
    {
        if (_pending is null) return;

        Publish(_pending);
        _pending = null;
    }

    // An update of another message, response, role or author than the text that waits, as the caller's
    // ToChatResponse tells them apart: an id the update leaves out belongs to the message going on
    private bool StartsAnother(ChatResponseUpdate update) =>
        Differs(update.MessageId, _pending!.MessageId)
        || Differs(update.ResponseId, _pending.ResponseId)
        || Differs(update.AuthorName, _pending.AuthorName)
        || (update.Role is { } role && _pending.Role is { } pending && role != pending);

    private static bool Differs(string? id, string? pending) =>
        id is { Length: > 0 } && pending is { Length: > 0 } && id != pending;

    private void Publish(ChatResponseUpdate update)
    {
        runs.Publish(conversation, update);
        _published = time.GetUtcNow();
    }
}
