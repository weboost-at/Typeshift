using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Saves by id and kind but loads the first kind saved under the id whatever kind is asked for, as a query by the id
// alone that takes the first row would
public sealed class LoadByIdAloneStore : ISettingsStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    private readonly ConcurrentDictionary<Guid, string> _firstKinds = [];

    public async Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _firstKinds.TryGetValue(id, out var first) ? await _store.LoadAsync(id, first, ct) : null;

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        _firstKinds.TryAdd(id, kind);
        return _store.SaveAsync(id, kind, json, loadedVersion, ct);
    }
}
