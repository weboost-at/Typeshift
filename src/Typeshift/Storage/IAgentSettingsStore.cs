namespace Typeshift;

/// <summary>
/// Keeps each agent's own settings, which every run of the agent applies: a JSON document per kind, guarded by a
/// version, with no id. Implement it for an app whose agents have one set of settings each; settings under a settings
/// id go through <see cref="ISettingsStore{TId}"/>.
/// </summary>
public interface IAgentSettingsStore
{
    /// <summary>
    /// Loads the document of a kind.
    /// </summary>
    /// <param name="kind">The document's kind, an agent's <c>Settings.Kind</c>.</param>
    /// <param name="ct">Cancels the load.</param>
    /// <returns>The document, or null when none is stored.</returns>
    Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct);

    /// <summary>
    /// Saves the document of a kind if the stored version is still <paramref name="loadedVersion"/>, 0 for none.
    /// </summary>
    /// <param name="kind">The document's kind.</param>
    /// <param name="json">The document's JSON, which a load returns as it is.</param>
    /// <param name="loadedVersion">
    /// The version <see cref="LoadAsync"/> returned to the caller, 0 when it found none: the old version, which the
    /// stored one must still equal, never the version to write.
    /// </param>
    /// <param name="ct">Cancels the save.</param>
    /// <returns>The new version the store wrote, which the caller passes to its next save.</returns>
    /// <exception cref="StoreConflictException">Another save came first.</exception>
    Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct);
}
