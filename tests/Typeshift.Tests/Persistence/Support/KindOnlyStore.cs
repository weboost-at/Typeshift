namespace Typeshift.Tests.Persistence.Support;

// Keys documents by the kind alone, as a query that forgets the id would: every id loads and saves the same document
public sealed class KindOnlyStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(Guid.Empty, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(Guid.Empty, kind, json, loadedVersion, cancellationToken);
}
