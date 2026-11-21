namespace Typeshift;

/// <summary>
/// Keeps conversations: the entries runs append, keyed by the conversation's id. It receives the conversation ids of
/// <typeparamref name="TId"/> that <c>WithConversationId</c> gives a host. Implement it alone when settings live
/// elsewhere, or <see cref="ITypeshiftStore{TId}"/> for both.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/implementing-a-store#append-to-a-conversation">Append to a
/// conversation</see>.
/// </summary>
/// <typeparam name="TId">
/// The id type of conversations. The library passes on the host's ids, and creates one only for a conversation a run
/// starts with <c>PersistAllConversations</c>.
/// </typeparam>
public interface IConversationStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads every entry of a conversation, summaries included, such as a chat UI shows, in the order they were
    /// appended (not by <see cref="ConversationEntry.At"/>, which can tie). Runs start from
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
    /// <returns>The entries, in the order they were appended; none for a conversation never stored.</returns>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Appends an entry to a conversation, creating it on the first. Runs append as they go, so this is the hot path.
    /// Store the entry as the JSON its <see cref="ConversationEntry.Serialize"/> writes. In one container, one run
    /// appends to a conversation at a time. Across containers, such as two instances of your app, refuse an entry
    /// whose <see cref="ConversationEntry.Sequence"/> the conversation holds already: the run that appended second
    /// then stops with a <c>ConversationBusyException</c>, so the two never interleave. An append that throws
    /// anything else is logged and not retried, and fails the run as it ends.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="ct">Cancels the append.</param>
    /// <returns>A task that completes once the entry is stored.</returns>
    /// <exception cref="StoreConflictException">
    /// The conversation holds an entry of this entry's <see cref="ConversationEntry.Sequence"/> already, for a store
    /// that refuses such an entry.
    /// </exception>
    Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct);

    /// <summary>
    /// Whether the store refuses an entry whose <see cref="ConversationEntry.Sequence"/> the conversation holds
    /// already, with a <see cref="StoreConflictException"/> and nothing stored, as a unique key on the conversation
    /// and the number does. <c>VerifyAsync</c> then checks that it does. Runs number their entries either way, and a
    /// store that does not refuse them keeps working: two instances of your app that run one conversation at once
    /// then interleave its entries.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/implementing-a-store#refuse-a-second-run">Refuse a
    /// second run</see>.
    /// </summary>
    bool RefusesTakenSequences => false;
}
