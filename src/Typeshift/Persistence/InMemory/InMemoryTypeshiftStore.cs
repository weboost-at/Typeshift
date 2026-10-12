using System.Collections.Concurrent;

namespace Typeshift.Persistence.InMemory;

/// <summary>
/// Keeps everything in memory, as JSON, for tests, samples and single-process hosts.
/// </summary>
/// <typeparam name="TId">The id type of your choice.</typeparam>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId> where TId : notnull
{
    private readonly ConcurrentDictionary<(Type Kind, TId Id), string> _documents = [];

    /// <inheritdoc />
    public Task<TData?> LoadAsync<TData>(TId id, CancellationToken ct) where TData : class, IStorable<TData> =>
        Task.FromResult(_documents.TryGetValue((typeof(TData), id), out var json) ? TData.Deserialize(json) : null);

    /// <inheritdoc />
    public Task SaveAsync<TData>(TId id, TData data, CancellationToken ct) where TData : class, IStorable<TData>
    {
        _documents[(typeof(TData), id)] = data.Serialize();
        return Task.CompletedTask;
    }
}
