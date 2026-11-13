using System.Collections.Concurrent;

namespace Typeshift.Tests.Persistence.Support;

// Loads an id's document as an agent's own when the agent has none, as one table read by kind without "no id" would;
// everything else works
public sealed class AgentLoadFallbackStore : ISettingsStore<Guid>, IAgentSettingsStore
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    private readonly ConcurrentDictionary<string, Guid> _ids = [];

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        _ids[kind] = id;
        return _store.SaveAsync(id, kind, json, loadedVersion, ct);
    }

    public async Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) =>
        await _store.LoadAsync(kind, ct)
        ?? (_ids.TryGetValue(kind, out var id) ? await _store.LoadAsync(id, kind, ct) : null);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(kind, json, loadedVersion, ct);
}
