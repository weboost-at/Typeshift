using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

/// <summary>
/// The conversations under ids of <typeparamref name="TId"/> running in this container. Inject the tracker of your
/// conversation ids' type, such as <c>ConversationTracker&lt;int&gt;</c>; the container has one for every id type, a
/// singleton.
/// More in the guide: <see href="https://weboost-at.github.io/Typeshift/guide/conversations#live-runs">Live runs</see>.
/// </summary>
/// <typeparam name="TId">The type of the conversation ids, as <c>WithConversationId</c> gave them.</typeparam>
public sealed class ConversationTracker<TId> where TId : notnull
{
    private readonly Lazy<LiveRuns> _runs;

    // D72: public, so the container makes the tracker of any id type it is asked for
    /// <summary>
    /// The tracker of the runtime's conversations, which the container creates.
    /// </summary>
    /// <param name="runtime">The runtime whose runs it tracks.</param>
    /// <exception cref="ArgumentNullException"><paramref name="runtime"/> is null.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ConversationTracker(TypeshiftRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        var services = runtime.Services;

        // D69, D72: beside a registered store, a tracker of an id type no store keeps would watch nothing forever, so
        // its first call fails as a run under such an id does
        _runs = new(() =>
        {
            services.GetRequiredService<StoreAccess>().ConversationStore<TId>();
            return services.GetRequiredService<LiveRuns>();
        });
    }

    /// <summary>
    /// The runs of conversations under ids of <typeparamref name="TId"/> going on now.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Your app registered stores, and none keeps conversations under ids of <typeparamref name="TId"/>.
    /// </exception>
    public IReadOnlyList<LiveRun<TId>> Runs => [.. _runs.Value.Runs.Where(IsOfIdType).Select(Live)];

    /// <summary>
    /// The runs of conversations under ids of <typeparamref name="TId"/> going on now, then each run as it starts and
    /// as it ends, until cancelled. A watcher that falls behind loses its oldest changes.
    /// </summary>
    /// <param name="ct">Ends the watch.</param>
    /// <returns>The runs; one that ended has <see cref="LiveRun{TId}.Ended"/> set.</returns>
    /// <exception cref="InvalidOperationException">
    /// Your app registered stores, and none keeps conversations under ids of <typeparamref name="TId"/>.
    /// </exception>
    public IAsyncEnumerable<LiveRun<TId>> WatchRunsAsync(CancellationToken ct) =>
        _runs.Value.WatchRunsAsync(ct)
            .Where(change => IsOfIdType(change.Run))
            .Select(change => Live(change.Run) with { Ended = change.Ended });

    /// <summary>
    /// The updates of a conversation's runs as they happen, until cancelled. A watcher that falls behind loses its
    /// oldest updates.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Ends the watch.</param>
    /// <returns>The updates.</returns>
    /// <exception cref="InvalidOperationException">
    /// Your app registered stores, and none keeps conversations under ids of <typeparamref name="TId"/>.
    /// </exception>
    public IAsyncEnumerable<ChatResponseUpdate> WatchAsync(TId conversation, CancellationToken ct) =>
        _runs.Value.WatchAsync(new StoreId<TId>(conversation), ct);

    /// <summary>
    /// Hands input to the conversation's run going on in this container. The run's middleware sees it first and may
    /// drop it. The model receives it with its next call, and the run answers it before it ends, unless the run
    /// fails, is cancelled or has spent its limit of model calls: the input is then stored with the run's end, and
    /// the conversation's next run reads it.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="input">The user's input.</param>
    /// <returns>
    /// False when no run of the conversation takes input here: none is going on, one still loads the conversation,
    /// or it is ending. Start a run with the input then; it waits for a run that is ending.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Your app registered stores, and none keeps conversations under ids of <typeparamref name="TId"/>.
    /// </exception>
    public bool TrySteer(TId conversation, string input) =>
        _runs.Value.TrySteer(new StoreId<TId>(conversation), new ChatMessage(ChatRole.User, input));

    /// <summary>
    /// Hands a message to the conversation's run going on in this container, as
    /// <see cref="TrySteer(TId, string)"/> does with text: a user message that may carry images or files, as a run's
    /// input may.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="input">The user's message.</param>
    /// <returns>
    /// False when no run of the conversation takes input here: none is going on, one still loads the conversation,
    /// or it is ending. Start a run with the message then.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// The message is not the user's, or carries approval contents, which a run does not take.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Your app registered stores, and none keeps conversations under ids of <typeparamref name="TId"/>.
    /// </exception>
    public bool TrySteer(TId conversation, ChatMessage input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // D80: what a run takes as input, steering takes too: a user message that answers nothing
        if (input.Role != ChatRole.User
            || input.Contents.Any(content => content is InputRequestContent or InputResponseContent))
        {
            throw new ArgumentException(
                "Steering input is a user message without approval contents: answer a decision by its id, with "
                + "AnswerAsync or RecordAnswersAsync.",
                nameof(input));
        }

        return _runs.Value.TrySteer(new StoreId<TId>(conversation), input);
    }

    // D72: a conversation of another id type is another tracker's
    private static bool IsOfIdType(ConversationRun run) => run.Conversation is StoreId<TId>;

    private static LiveRun<TId> Live(ConversationRun run) =>
        new(((StoreId<TId>)run.Conversation!).Id, run.Agent, run.Model, run.StartedAt);
}

/// <summary>
/// A run of a conversation in this container.
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
/// How runs publish their live updates, which a <see cref="ConversationTracker{TId}"/> reads with <c>WatchAsync</c>.
/// Live updates are on without a setting; set these as <c>TypeshiftOptions.LiveUpdates</c> through
/// <c>AddTypeshift().WithOptions(...)</c>.
/// </summary>
public sealed class LiveUpdateOptions
{
    /// <summary>
    /// Text, reasoning and usage updates are merged and published at most this often; others at once. 100
    /// milliseconds unless set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan PublishInterval
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// The updates, or run starts and ends, a watcher may fall behind by; beyond it, the oldest are dropped. A run
    /// never waits for a watcher. 256 unless set.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is below 1.</exception>
    public int MaxPendingUpdates
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 256;
}
