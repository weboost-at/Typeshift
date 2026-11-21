namespace Typeshift.Tests.Persistence.Support;

// Writes first and compares the version after, as an upsert that counts an update as two rows can
public sealed class WriteThenConflictStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(id, kind, cancellationToken);

    public async Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken)
    {
        var stored = await _store.LoadAsync(id, kind, cancellationToken);
        var saved = await _store.SaveAsync(id, kind, json, stored?.Version ?? 0, cancellationToken);

        return (stored?.Version ?? 0) == loadedVersion
            ? saved
            : throw new StoreConflictException($"{kind} {id} was saved after version {loadedVersion}.");
    }
}
