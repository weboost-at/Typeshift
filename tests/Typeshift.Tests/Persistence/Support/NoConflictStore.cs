namespace Typeshift.Tests.Persistence.Support;

// Saves over whatever is stored, ignoring the revision it is given
public sealed class NoConflictStore : ISettingsStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public async Task<long> SaveAsync(Guid id, string kind, string json, long revision, CancellationToken ct)
    {
        var stored = await _store.LoadAsync(id, kind, ct);
        return await _store.SaveAsync(id, kind, json, stored?.Revision ?? 0, ct);
    }
}
