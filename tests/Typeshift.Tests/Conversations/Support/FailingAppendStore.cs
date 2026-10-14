namespace Typeshift.Tests.Conversations.Support;

// Loads like a store in memory and fails every append, as a store that went down mid-run
public sealed class FailingAppendStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<Stored<TData>?> LoadAsync<TData>(Guid id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        _store.LoadAsync<TData>(id, ct);

    public Task<long> SaveAsync<TData>(Guid id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        _store.SaveAsync(id, data, revision, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        throw new IOException("The store is down.");
}
