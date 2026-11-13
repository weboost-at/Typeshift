namespace Typeshift;

/// <summary>
/// Keeps the decisions of conversations: one JSON document per conversation, guarded by a version, as a settings
/// document is. The document holds what a run asked your app and has not carried out yet, and the tools your app
/// approved for the conversation. It receives the conversation ids of <typeparamref name="TId"/> that
/// <c>WithConversationId</c> gives a host. Implement it beside <see cref="IConversationStore{TId}"/>, or
/// <see cref="ITypeshiftStore{TId}"/> for settings, conversations and decisions in one.
/// </summary>
/// <typeparam name="TId">
/// The id type of conversations. The library passes on the host's ids, and creates one only for a conversation a run
/// starts with <c>PersistAllConversations</c>.
/// </typeparam>
public interface IDecisionStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads the decisions document of a conversation.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The document, or null when none is stored.</returns>
    Task<StoredDocument?> LoadDecisionsAsync(TId conversation, CancellationToken ct);

    /// <summary>
    /// Saves the decisions document of a conversation if the stored version is still
    /// <paramref name="loadedVersion"/>, 0 for none. Check the version and write in one statement, as for a settings
    /// document: this check is what lets an approved call start only once.
    /// </summary>
    /// <param name="conversation">The conversation's id.</param>
    /// <param name="json">The document's JSON, which a load returns as it is.</param>
    /// <param name="loadedVersion">
    /// The version <see cref="LoadDecisionsAsync"/> returned to the caller, 0 when it found none: the old version,
    /// which the stored one must still equal, never the version to write.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>
    /// The new version the store wrote, which the caller passes to its next save: never 0 and never a version this
    /// document had before.
    /// </returns>
    /// <exception cref="StoreConflictException">Another save came first.</exception>
    Task<long> SaveDecisionsAsync(TId conversation, string json, long loadedVersion, CancellationToken ct);
}
