using System.Collections.Concurrent;
using Typeshift.Persistence;

namespace Typeshift.Tests.Conversations.Support;

// The in-memory store, plus deleting and a hold on saves, so a test can change what sessions find when they end
public sealed class ConversationTestStore : ITypeshiftStore<Guid>
{
    private readonly ConcurrentDictionary<(Type Kind, Guid Id), string> _documents = [];
    private readonly TaskCompletionSource _saving = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Every save waits for it; a faulted task fails the saves
    public Task SavesWaitFor { get; set; } = Task.CompletedTask;

    // Completes when the first save reaches the store
    public Task Saving => _saving.Task;

    public Task<TData?> LoadAsync<TData>(Guid id, CancellationToken ct) where TData : class, IStorable<TData> =>
        Task.FromResult(_documents.TryGetValue((typeof(TData), id), out var json) ? TData.Deserialize(json) : null);

    public async Task SaveAsync<TData>(Guid id, TData data, CancellationToken ct) where TData : class, IStorable<TData>
    {
        _saving.TrySetResult();
        await SavesWaitFor;
        _documents[(typeof(TData), id)] = data.Serialize();
    }

    public void Delete<TData>(Guid id) => _documents.TryRemove((typeof(TData), id), out _);
}
