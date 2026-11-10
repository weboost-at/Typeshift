namespace Typeshift;

/// <summary>
/// Keeps conversations: the entries runs append, keyed by the conversation's id. Each project sees it as
/// <c>IConversationStore</c>, an alias the generator writes for the build's id type. Implement it alone when settings
/// live elsewhere, or <see cref="ITypeshiftStore{TId}"/> for both.
/// </summary>
/// <typeparam name="TId">
/// The id type of conversations; the library never creates ids, it passes on the host's.
/// </typeparam>
public interface IConversationStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads every entry of a conversation, oldest first, summaries included, such as a chat UI shows. Runs start from
    /// <see cref="LoadConversationSinceCompactionAsync"/>, whose default calls this method.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries; none for a conversation never stored.</returns>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Loads what a run reads: the conversation's last summary and the entries after it, or every entry when
    /// compaction never summarized it. The default loads the whole conversation through
    /// <see cref="LoadConversationAsync"/> and drops what came before the summary. Implement it in the store when the
    /// store can start at the last summary, so a long conversation is not read whole on every run.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries, oldest first; none for a conversation never stored.</returns>
    async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        TId conversation,
        CancellationToken ct)
    {
        var entries = await LoadConversationAsync(conversation, ct);
        return ConversationHistory.SinceSummary(entries);
    }

    /// <summary>
    /// Appends an entry to a conversation, creating it on the first. Runs append as they go, so this is the hot path.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="ct">Cancels the append.</param>
    /// <returns>A task that completes once the entry is stored.</returns>
    Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct);
}
