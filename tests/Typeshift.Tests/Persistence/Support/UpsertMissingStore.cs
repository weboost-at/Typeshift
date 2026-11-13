using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Compares the version of a stored document, but writes a document that is not stored whatever version it is given,
// as an update that inserts the row it does not find would
public sealed class UpsertMissingStore : ISettingsStore<Guid>
{
    private readonly ConcurrentDictionary<(Guid Id, string Kind), StoredDocument> _rows = [];

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        Task.FromResult(_rows.GetValueOrDefault((id, kind)));

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        if (_rows.GetValueOrDefault((id, kind)) is { } stored && stored.Version != loadedVersion)
            throw new StoreConflictException($"{kind} {id} was saved after version {loadedVersion}.");

        _rows[(id, kind)] = new StoredDocument(json, loadedVersion + 1);
        return Task.FromResult(loadedVersion + 1);
    }
}
