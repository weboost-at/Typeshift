namespace Typeshift.Tests.Persistence.Support;

// Refuses a second first save but saves over a stored document whatever version it is given, as an UPDATE without
// the version in its WHERE would
public sealed class StaleVersionStore : ISettingsStore<Guid>
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
        var version = loadedVersion == 0 || stored is null ? loadedVersion : stored.Version;

        return await _store.SaveAsync(id, kind, json, version, cancellationToken);
    }
}
