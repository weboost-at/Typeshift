namespace Typeshift.Tests.Persistence.Support;

// Keeps settings in memory and no conversations, as a store of settings in a database of their own
public sealed class SettingsOnlyStore : ISettingsStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, int version, long revision, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, version, revision, ct);
}
