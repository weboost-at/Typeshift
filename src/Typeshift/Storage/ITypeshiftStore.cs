namespace Typeshift;

/// <summary>
/// The one store Typeshift persists through. Each project sees it as <c>ITypeshiftStore</c>, an alias the generator
/// writes for the build's id type (<c>TypeshiftIdType</c>, <see cref="Guid"/> by default).
/// </summary>
/// <typeparam name="TId">
/// The id type of settings and conversations; the library never creates ids, it passes on the host's.
/// </typeparam>
public interface ITypeshiftStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads the document of a kind stored under an id.
    /// </summary>
    /// <param name="kind">
    /// The document's kind, an agent's <c>Settings.Kind</c>; one id holds a document of each kind.
    /// </param>
    /// <param name="id">The id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The document, or null when none is stored.</returns>
    Task<StoredDocument?> LoadAsync(string kind, TId id, CancellationToken ct);

    /// <summary>
    /// Saves the document of a kind under an id if the stored revision is still <paramref name="revision"/>, 0 for
    /// none.
    /// </summary>
    /// <param name="kind">The document's kind.</param>
    /// <param name="id">The id.</param>
    /// <param name="json">The document's JSON.</param>
    /// <param name="version">The schema version the JSON was written under, which a load returns with it.</param>
    /// <param name="revision">The revision the caller loaded, 0 when it found none.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new revision.</returns>
    /// <exception cref="StoreConflictException">Another save came first.</exception>
    Task<long> SaveAsync(string kind, TId id, string json, int version, long revision, CancellationToken ct);

    /// <summary>
    /// Loads the entries of a conversation, oldest first.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The entries; none for a conversation never stored.</returns>
    Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Loads the entries a run starts from: the conversation's last summary and the entries after it, or every entry
    /// when compaction never summarized it. Override it where the store finds the last summary without reading the
    /// rest; this one loads the whole conversation.
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

/// <summary>
/// A document as a store keeps it: the JSON, the schema version it was written under, and the revision a save must
/// name.
/// </summary>
/// <param name="Json">The JSON.</param>
/// <param name="Version">The schema version.</param>
/// <param name="Revision">The revision.</param>
public sealed record StoredDocument(string Json, int Version, long Revision);

/// <summary>
/// A document read back as its type, and the revision a save must name.
/// </summary>
/// <typeparam name="TData">The document's type.</typeparam>
/// <param name="Data">The document.</param>
/// <param name="Revision">Its revision.</param>
public sealed record Stored<TData>(TData Data, long Revision);

/// <summary>
/// A save that lost to another save of the same document.
/// </summary>
/// <param name="message">What was saved.</param>
/// <param name="inner">The store's own exception, if any.</param>
public sealed class StoreConflictException(string message, Exception? inner = null) : Exception(message, inner);
