namespace Typeshift.Tests.Persistence.Support;

// Loads a document under another version than its save returned, as a store that reads a counter of its own would
public sealed class OtherVersionStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public async Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        await _store.LoadAsync(id, kind, ct) is { } stored ? stored with { Version = stored.Version + 1 } : null;

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);
}
