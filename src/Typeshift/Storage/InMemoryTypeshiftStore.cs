using System.Collections.Concurrent;

namespace Typeshift;

/// <summary>
/// A store in memory, for tests and samples. Each project sees it as <c>InMemoryTypeshiftStore</c>. It keeps each
/// document as its JSON, so a load returns a copy and a round trip is tested on every save.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId> where TId : notnull
{
    private readonly ConcurrentDictionary<(string Kind, TId Id), (string Json, int Version, long Revision)> _documents =
        [];

    /// <inheritdoc />
    public Task<Stored<TData>?> LoadAsync<TData>(TId id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        Task.FromResult(_documents.TryGetValue((TData.Kind, id), out var stored)
            ? new Stored<TData>(TData.Deserialize(stored.Json, stored.Version), stored.Revision)
            : null);

    /// <inheritdoc />
    public Task<long> SaveAsync<TData>(TId id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        var key = (TData.Kind, id);
        var saved = (data.Serialize(), TData.Version, revision + 1);

        var won = revision == 0
            ? _documents.TryAdd(key, saved)
            : _documents.TryGetValue(key, out var stored)
                && stored.Revision == revision
                && _documents.TryUpdate(key, saved, stored);

        return won
            ? Task.FromResult(revision + 1)
            : throw new StoreConflictException($"The {TData.Kind} of {id} was saved since revision {revision}.");
    }
}
