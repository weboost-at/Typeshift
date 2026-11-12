namespace Typeshift;

/// <summary>
/// What every run of the app does, set with <c>AddTypeshift().WithOptions(...)</c>.
/// </summary>
public sealed class TypeshiftOptions
{
    private Func<StoreId>? _newConversationId;

    /// <summary>
    /// Whether a run without a conversation id stores a conversation of its own, under a new id, which the response's
    /// <c>ConversationId</c> names. Off by default; a sub-agent's run never starts one.
    /// </summary>
    public bool PersistAllConversations { get; set; }

    /// <summary>
    /// Gives new conversations ids of your conversation store's type; without it, they get a <see cref="Guid"/> of
    /// version 7, ordered by the time.
    /// </summary>
    /// <param name="create">Returns a new id, one no conversation has, each time it is called.</param>
    /// <typeparam name="TId">The id type of your conversation store.</typeparam>
    public void NewConversationIds<TId>(Func<TId> create) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(create);
        _newConversationId = () => new StoreId<TId>(create());
    }

    // D73: an id the app chose, or one ordered by the clock the host injects
    internal StoreId NewConversationId(TimeProvider time) =>
        _newConversationId?.Invoke() ?? new StoreId<Guid>(Guid.CreateVersion7(time.GetUtcNow()));
}
