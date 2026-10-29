namespace Typeshift;

/// <summary>
/// Keeps the agents' settings: a JSON document per settings id and kind, guarded by a version. Each project sees it
/// as <c>ISettingsStore</c>, an alias the generator writes for the build's id type. Implement it alone when
/// conversations live elsewhere, or <see cref="ITypeshiftStore{TId}"/> for both.
/// </summary>
/// <typeparam name="TId">The id type of settings; the library never creates ids, it passes on the host's.</typeparam>
public interface ISettingsStore<TId> where TId : notnull
{
    /// <summary>
    /// Loads the document of a kind stored under an id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="kind">
    /// The document's kind, an agent's <c>Settings.Kind</c>; one id holds a document of each kind.
    /// </param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The document, or null when none is stored.</returns>
    Task<StoredDocument?> LoadAsync(TId id, string kind, CancellationToken ct);

    /// <summary>
    /// Saves the document of a kind under an id if the stored version is still <paramref name="expectedVersion"/>, 0
    /// for none.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="kind">The document's kind.</param>
    /// <param name="json">The document's JSON, which a load returns as it is.</param>
    /// <param name="expectedVersion">The version the caller loaded, 0 when it found none.</param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new version.</returns>
    /// <exception cref="StoreConflictException">Another save came first.</exception>
    Task<long> SaveAsync(TId id, string kind, string json, long expectedVersion, CancellationToken ct);
}

/// <summary>
/// A document as a store keeps it: the JSON, and the version a save must name.
/// </summary>
/// <param name="Json">The JSON.</param>
/// <param name="Version">The version.</param>
public sealed record StoredDocument(string Json, long Version);

/// <summary>
/// A document read back as its type, and the version a save must name.
/// </summary>
/// <typeparam name="TData">The document's type.</typeparam>
/// <param name="Data">The document.</param>
/// <param name="Version">Its version.</param>
public sealed record Stored<TData>(TData Data, long Version);

/// <summary>
/// A save that lost to another save of the same document.
/// </summary>
/// <param name="message">What was saved.</param>
/// <param name="inner">The store's own exception, if any.</param>
public sealed class StoreConflictException(string message, Exception? inner = null) : Exception(message, inner);
