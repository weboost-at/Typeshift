namespace Typeshift;

/// <summary>
/// What every run of the app does, set with <c>AddTypeshift().WithOptions(...)</c>.
/// </summary>
public sealed class TypeshiftOptions
{
    private Func<StoreId>? _newConversationId;

    /// <summary>
    /// Whether a run with input and without a conversation id stores a conversation of its own, under a new id, which
    /// the response's <c>ConversationId</c> names. Off by default; a sub-agent's run never starts one.
    /// </summary>
    public bool PersistAllConversations { get; set; }

    /// <summary>
    /// Gives new conversations ids of your conversation store's type. Without it, they get a <see cref="Guid"/> of
    /// version 7, ordered by the time, or its text when the build's id type is <see cref="string"/>; a build of any
    /// other id type needs it. Runs call it at the same time, so make it thread-safe.
    /// </summary>
    /// <param name="create">Returns a new id, one no conversation has, each time it is called.</param>
    /// <typeparam name="TId">The id type of your conversation store.</typeparam>
    public void NewConversationIds<TId>(Func<TId> create) where TId : notnull
    {
        ArgumentNullException.ThrowIfNull(create);
        _newConversationId = () => new StoreId<TId>(create());
    }

    // D73: an id the app chose; null leaves it to the build's id type
    internal StoreId? NewConversationId() => _newConversationId?.Invoke();
}
