namespace Typeshift.Tests.Persistence.Support;

// Saves over whatever is stored, ignoring the version it is given
public sealed class NoConflictStore : ISettingsStore<Guid>
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
        return await _store.SaveAsync(id, kind, json, stored?.Version ?? 0, cancellationToken);
    }
}
