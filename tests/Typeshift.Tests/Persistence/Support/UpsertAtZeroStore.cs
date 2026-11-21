namespace Typeshift.Tests.Persistence.Support;

// Saves over a stored document at version 0, as an INSERT that updates on a conflict would; later versions are
// compared
public sealed class UpsertAtZeroStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public async Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        var stored = await _store.LoadAsync(id, kind, ct);
        var version = loadedVersion == 0 ? stored?.Version ?? 0 : loadedVersion;

        return await _store.SaveAsync(id, kind, json, version, ct);
    }
}
