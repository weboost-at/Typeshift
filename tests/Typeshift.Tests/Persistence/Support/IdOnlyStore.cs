namespace Typeshift.Tests.Persistence.Support;

// Keys documents by id alone, as a store that missed that one id holds a document per agent
public sealed class IdOnlyStore : ISettingsStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, "settings", ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, "settings", json, loadedVersion, ct);
}
