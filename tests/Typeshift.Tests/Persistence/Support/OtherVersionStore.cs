namespace Typeshift.Tests.Persistence.Support;

// Loads a document under another version than its save returned, as a store that reads a counter of its own would
public sealed class OtherVersionStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public async Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        await _store.LoadAsync(
            id,
            kind,
            cancellationToken) is { } stored ? stored with { Version = stored.Version + 1 } : null;

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(id, kind, json, loadedVersion, cancellationToken);
}
