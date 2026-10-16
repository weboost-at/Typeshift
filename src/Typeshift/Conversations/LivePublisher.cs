using Microsoft.Extensions.AI;

namespace Typeshift;

// D47: text, reasoning and usage merge until the interval passes; anything else goes out at once, after them
internal sealed class LivePublisher(
    LiveRuns runs,
    object conversation,
    RealtimeTrackingOptions options,
    TimeProvider time)
{
    private ChatResponseUpdate? _pending;
    private DateTimeOffset _published = DateTimeOffset.MinValue;

    public void Add(ChatResponseUpdate update)
    {
        if (!update.Contents.All(content => content is TextContent or TextReasoningContent or UsageContent))
        {
            Flush();
            Publish(update);
            return;
        }

        if (_pending is null)
        {
            // A copy with the first update's ids, so a watcher groups the merged text as the run's caller does
            _pending = update.Clone();
            _pending.Contents = [.. update.Contents];
        }
        else
        {
            _pending.Contents = [.. _pending.Contents, .. update.Contents];
        }

        if (time.GetUtcNow() - _published >= options.PublishInterval) Flush();
    }

    public void Flush()
    {
        if (_pending is null) return;

        Publish(_pending);
        _pending = null;
    }

    private void Publish(ChatResponseUpdate update)
    {
        runs.Publish(conversation, update);
        _published = time.GetUtcNow();
    }
}
