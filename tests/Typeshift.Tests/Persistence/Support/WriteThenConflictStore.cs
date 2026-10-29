namespace Typeshift.Tests.Persistence.Support;

// Writes first and compares the revision after, as an upsert that counts an update as two rows can
public sealed class WriteThenConflictStore : ISettingsStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public async Task<long> SaveAsync(Guid id, string kind, string json, long revision, CancellationToken ct)
    {
        var stored = await _store.LoadAsync(id, kind, ct);
        var saved = await _store.SaveAsync(id, kind, json, stored?.Revision ?? 0, ct);

        return (stored?.Revision ?? 0) == revision
            ? saved
            : throw new StoreConflictException($"{kind} {id} was saved after revision {revision}.");
    }
}
