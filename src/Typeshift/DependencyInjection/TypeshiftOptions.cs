namespace Typeshift;

/// <summary>
/// What every run of the app does, set with <c>AddTypeshift().WithOptions(...)</c>.
/// </summary>
public sealed class TypeshiftOptions
{
    private Func<StoreId>? _newConversationId;

    private Func<string>? _newTextId;

    /// <summary>
    /// Whether a run with input and without a conversation id stores a conversation of its own, under a new id, which
    /// the response's <c>ConversationId</c> names. Off by default; a sub-agent's run never starts one.
    /// </summary>
    public bool PersistAllConversations { get; set; }

    /// <summary>
    /// What a run does when the store fails to append a step of its conversation. By default it fails, with a
    /// <see cref="ConversationStoreException"/> that holds its answer; <see cref="StoreFailure.ContinueRun"/> has it
    /// return the answer, which <c>response.ConversationIncomplete</c> then marks.
    /// </summary>
    public StoreFailure StoreFailure { get; set; }

    // D74: on from the start, so these only tune them
    /// <summary>
    /// How runs publish their live updates: how often merged text goes out, and how far a watcher may fall behind,
    /// as in <c>WithOptions(options =&gt; options.LiveUpdates.MaxPendingUpdates = 1024)</c>.
    /// </summary>
    public LiveUpdateOptions LiveUpdates { get; } = new();

    /// <summary>
    /// Gives new conversations ids of your conversation store's type. Without it, they get a <see cref="Guid"/> of
    /// version 7, ordered by the time, and need an <c>IConversationStore&lt;Guid&gt;</c>. A raw-path call names its
    /// conversation in <c>ChatOptions.ConversationId</c>, a string, so a conversation it starts gets this id only when
    /// it is a <see cref="string"/>, and the text of such a <see cref="Guid"/> otherwise. Runs call it at the same
    /// time, so make it thread-safe.
    /// </summary>
    /// <param name="create">Returns a new id, one no conversation has, each time it is called.</param>
    /// <typeparam name="TId">The id type of your conversation store.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is null.</exception>
    public void NewConversationIds<TId>(Func<TId> create) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(create);
        _newConversationId = () => new StoreId<TId>(create());
        _newTextId = create as Func<string>;
    }

    // D88: ten minutes, after which a client no run used is disposed
    private TimeSpan _providerClientIdleTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long Typeshift keeps the client of a <see cref="ChatProvider"/> no run used before it disposes it: 10
    /// minutes by default. A later run handed an equal document connects again. Clients your app registered are
    /// never disposed.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan ProviderClientIdleTime
    {
        get => _providerClientIdleTime;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            _providerClientIdleTime = value;
        }
    }

    // D63: the key WithDefaultProvider names, which a run that names no provider takes
    internal ProviderId? DefaultProvider { get; set; }

    // D73: an id the app chose, or a Guid of version 7, ordered by the clock the host injects
    internal StoreId NewConversationId(TimeProvider time) =>
        _newConversationId?.Invoke() ?? new StoreId<Guid>(Guid.CreateVersion7(time.GetUtcNow()));

    // D73: the raw path's, a string, which ChatOptions.ConversationId names again
    internal StoreId NewRawConversationId(TimeProvider time) =>
        new StoreId<string>(_newTextId?.Invoke() ?? Guid.CreateVersion7(time.GetUtcNow()).ToString());
}
