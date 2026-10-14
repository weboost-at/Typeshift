using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// The conversations running in this process. Each project sees it as <c>ConversationTracker</c>, an alias for the
/// build's id type.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class ConversationTracker<TId> where TId : notnull
{
    private readonly LiveRuns _runs;
    private readonly RealtimeTrackingOptions? _realtime;

    internal ConversationTracker(LiveRuns runs, RealtimeTrackingOptions? realtime)
    {
        _runs = runs;
        _realtime = realtime;
    }

    /// <summary>
    /// The runs of conversations going on now.
    /// </summary>
    public IReadOnlyList<LiveRun<TId>> Runs =>
        [.. _runs.Runs.Select(run => new LiveRun<TId>((TId)run.Conversation!, run.Agent, run.Model, run.StartedAt))];

    /// <summary>
    /// The updates of a conversation's streaming runs as they happen, until cancelled. A watcher that falls behind
    /// loses its oldest updates.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Ends the watch.</param>
    /// <returns>The updates.</returns>
    /// <exception cref="InvalidOperationException">Live updates are off.</exception>
    public IAsyncEnumerable<ChatResponseUpdate> WatchAsync(TId conversation, CancellationToken ct)
    {
        var realtime = _realtime
            ?? throw new InvalidOperationException(
                "Live updates are off: call WithRealtimeTracking() after AddTypeshift() to watch conversations.");

        return _runs.WatchAsync(conversation, realtime.MaxPendingUpdates, ct);
    }

    /// <summary>
    /// Steers a run going on in this process: the input reaches the model before its next call, and the run does not
    /// end before it has. The input is stored with the conversation.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="input">The user's input.</param>
    /// <returns>
    /// False when no run of the conversation is going on here, or it is ending; start a run with the input instead.
    /// </returns>
    public bool TrySteer(TId conversation, string input) => _runs.TrySteer(conversation, input);
}

/// <summary>
/// A run going on now.
/// </summary>
/// <param name="Conversation">The conversation's id.</param>
/// <param name="Agent">The name of the agent that answers.</param>
/// <param name="Model">The model it answers with.</param>
/// <param name="StartedAt">When the run started.</param>
/// <typeparam name="TId">The id type.</typeparam>
public sealed record LiveRun<TId>(TId Conversation, string Agent, string Model, DateTimeOffset StartedAt);

/// <summary>
/// How live updates are published.
/// </summary>
public sealed record RealtimeTrackingOptions
{
    /// <summary>
    /// Text, reasoning and usage updates are merged and published at most this often; others at once.
    /// </summary>
    public TimeSpan PublishInterval { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The updates a watcher may fall behind by; beyond it, the oldest are dropped. A run never waits for a watcher.
    /// </summary>
    public int MaxPendingUpdates { get; init; } = 256;
}
