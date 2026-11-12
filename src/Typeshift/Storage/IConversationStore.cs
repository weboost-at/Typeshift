namespace Typeshift;

/// <summary>
/// Keeps conversations: the entries runs append, keyed by the conversation's id. It receives the conversation ids of
/// <typeparamref name="TId"/> that <c>WithConversationId</c> gives a host. Implement it alone when settings live
/// elsewhere, or <see cref="ITypeshiftStore{TId}"/> for both.
/// </summary>
/// <typeparam name="TId">
/// The id type of conversations. The library passes on the host's ids, and creates one only for a conversation a run
/// starts with <c>PersistAllConversations</c>.
/// </typeparam>
public interface IConversationStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads every entry of a conversation, oldest first, summaries included, such as a chat UI shows. Runs start from
    /// <see cref="LoadConversationSinceCompactionAsync"/> instead.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries; none for a conversation never stored.</returns>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Loads what every run reads first: the conversation's last summary and the entries after it, or every entry when
    /// compaction never summarized it. An agent that compacts writes a summary, and with this load a long conversation
    /// is not read whole on every run, so start the store's query at the last summary. A store that cannot loads the
    /// whole conversation and returns <c>entries.SinceLastSummary()</c>.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries, oldest first; none for a conversation never stored.</returns>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Appends an entry to a conversation, creating it on the first. Runs append as they go, so this is the hot path.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="ct">Cancels the append.</param>
    /// <returns>A task that completes once the entry is stored.</returns>
    Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct);
}
