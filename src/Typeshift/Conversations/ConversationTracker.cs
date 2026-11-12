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
    private readonly RealtimeTrackingOptions _realtime;

    internal ConversationTracker(LiveRuns runs, RealtimeTrackingOptions realtime)
    {
        _runs = runs;
        _realtime = realtime;
    }

    /// <summary>
    /// The runs going on now of conversations under ids of <typeparamref name="TId"/>.
    /// </summary>
    public IReadOnlyList<LiveRun<TId>> Runs => [.. _runs.Runs.Where(IsOfIdType).Select(Live)];

    /// <summary>
    /// The runs of conversations going on now, then each run as it starts and as it ends, until cancelled. A watcher
    /// that falls behind loses its oldest changes.
    /// </summary>
    /// <param name="ct">Ends the watch.</param>
    /// <returns>The runs; one that ended has <see cref="LiveRun{TId}.Ended"/> set.</returns>
    public IAsyncEnumerable<LiveRun<TId>> WatchRunsAsync(CancellationToken ct) =>
        _runs.WatchRunsAsync(_realtime.MaxPendingUpdates, ct)
            .Where(change => IsOfIdType(change.Run))
            .Select(change => Live(change.Run) with { Ended = change.Ended });

    /// <summary>
    /// The updates of a conversation's streaming runs as they happen, until cancelled. A watcher that falls behind
    /// loses its oldest updates.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Ends the watch.</param>
    /// <returns>The updates.</returns>
    public IAsyncEnumerable<ChatResponseUpdate> WatchAsync(TId conversation, CancellationToken ct) =>
        WatchAsync<TId>(conversation, ct);

    /// <summary>
    /// The updates of the streaming runs of a conversation under an id of any type, as
    /// <c>WithConversationId</c> gave it, as they happen, until cancelled. A watcher that falls behind loses its oldest
    /// updates.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Ends the watch.</param>
    /// <typeparam name="TConversation">The id's type.</typeparam>
    /// <returns>The updates.</returns>
    public IAsyncEnumerable<ChatResponseUpdate> WatchAsync<TConversation>(
        TConversation conversation,
        CancellationToken ct)
        where TConversation : notnull =>
        _runs.WatchAsync(new StoreId<TConversation>(conversation), _realtime.MaxPendingUpdates, ct);

    /// <summary>
    /// Steers a run going on in this process: the input reaches the model before its next call, and the run does not
    /// end before it has. The input is stored with the conversation.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="input">The user's input.</param>
    /// <returns>
    /// False when no run of the conversation is going on here, or it is ending; start a run with the input instead.
    /// </returns>
    public bool TrySteer(TId conversation, string input) => TrySteer<TId>(conversation, input);

    /// <summary>
    /// Steers a run of a conversation under an id of any type, as <c>WithConversationId</c> gave it: the input reaches
    /// the model before its next call, and the run does not end before it has. The input is stored with the
    /// conversation.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="input">The user's input.</param>
    /// <typeparam name="TConversation">The id's type.</typeparam>
    /// <returns>
    /// False when no run of the conversation is going on here, or it is ending; start a run with the input instead.
    /// </returns>
    public bool TrySteer<TConversation>(TConversation conversation, string input) where TConversation : notnull =>
        _runs.TrySteer(new StoreId<TConversation>(conversation), input);

    // D72: a conversation of another id type is another tracker's
    private static bool IsOfIdType(ConversationRun run) => run.Conversation is StoreId<TId>;

    private static LiveRun<TId> Live(ConversationRun run) =>
        new(((StoreId<TId>)run.Conversation!).Id, run.Agent, run.Model, run.StartedAt);
}

/// <summary>
/// A run of a conversation in this process.
/// </summary>
/// <param name="Conversation">The conversation's id.</param>
/// <param name="Agent">The name of the agent that answers.</param>
/// <param name="Model">The model it answers with.</param>
/// <param name="StartedAt">When the run started.</param>
/// <typeparam name="TId">The id type.</typeparam>
public sealed record LiveRun<TId>(TId Conversation, string Agent, string Model, DateTimeOffset StartedAt)
{
    /// <summary>
    /// Whether the run has ended; only <see cref="ConversationTracker{TId}.WatchRunsAsync"/> reports runs that ended.
    /// </summary>
    public bool Ended { get; init; }
}

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
    /// The updates, or run starts and ends, a watcher may fall behind by; beyond it, the oldest are dropped. A run
    /// never waits for a watcher.
    /// </summary>
    public int MaxPendingUpdates { get; init; } = 256;
}
