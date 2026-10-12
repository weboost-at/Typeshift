using System.Collections.Immutable;
using Microsoft.Extensions.AI;

namespace Typeshift.Conversations;

/// <summary>
/// A streaming session: it publishes its updates while it runs.
/// </summary>
public sealed class RealtimeTrackedConversationSession : TrackedConversationSession
{
    // Text arrives in many small deltas, so it is published no more often than this
    internal static readonly TimeSpan PublishInterval = TimeSpan.FromMilliseconds(100);

    internal static readonly TimeSpan SubscriberLimit = TimeSpan.FromMilliseconds(250);

    private readonly List<ChatResponseUpdate> _updates = [];
    private long _published;

    internal RealtimeTrackedConversationSession(ConversationStart start, StoredConversation? resumed, TimeProvider time)
        : base(start, resumed, time) =>
        _published = time.GetTimestamp();

    /// <summary>
    /// The updates published so far.
    /// </summary>
    public ImmutableList<ChatResponseUpdate> Updates { get; private set; } = [];

    /// <summary>
    /// Raised when new updates are published; handlers must honour the token, they get 250 ms.
    /// </summary>
    public event Func<CancellationToken, Task>? OnSessionUpdated;

    /// <summary>
    /// Raised once when the session completes; handlers must honour the token, they get 250 ms.
    /// </summary>
    public event Func<CancellationToken, Task>? OnSessionCompleted;

    // Anything but text, reasoning or usage is published at once; text once the interval has passed
    internal bool ShouldPublish =>
        IsNotable(_updates[^1])
        || (_updates.Count > Updates.Count && Time.GetElapsedTime(_published) >= PublishInterval);

    internal void Track(ChatResponseUpdate update) => _updates.Add(update);

    internal async Task PublishAsync()
    {
        if (_updates.Count == Updates.Count) return;

        _published = Time.GetTimestamp();
        Updates = [.. _updates];
        await Subscribers.RaiseAsync(OnSessionUpdated, SubscriberLimit, Time);
    }

    internal override async Task<ConversationSession> CompleteAsync(ChatResponse response)
    {
        await Subscribers.RaiseAsync(OnSessionCompleted, SubscriberLimit, Time);
        return await base.CompleteAsync(response);
    }

    private static bool IsNotable(ChatResponseUpdate update) =>
        update.Contents.Any(content => content is not (TextContent or TextReasoningContent or UsageContent));
}
