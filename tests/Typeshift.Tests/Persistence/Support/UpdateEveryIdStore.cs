using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Inserts and loads by id and kind, but an update writes every id's document of the kind, as an UPDATE without the
// id in its WHERE would
public sealed class UpdateEveryIdStore : ISettingsStore<Guid>
{
    private readonly ConcurrentDictionary<(Guid Id, string Kind), StoredDocument> _rows = [];

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        Task.FromResult(_rows.GetValueOrDefault((id, kind)));

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        if ((_rows.GetValueOrDefault((id, kind))?.Version ?? 0) != loadedVersion)
            throw new StoreConflictException($"{kind} {id} was saved after version {loadedVersion}.");

        var saved = new StoredDocument(json, loadedVersion + 1);
        var written = loadedVersion == 0 ? [(id, kind)] : _rows.Keys.Where(row => row.Kind == kind).ToList();

        foreach (var row in written) _rows[row] = saved;
        return Task.FromResult(saved.Version);
    }
}
