namespace Typeshift.Tests.Conversations.Support;

// A store in memory that counts the whole conversations it loaded, as a store whose conversations grow long would
public sealed class CountingLoadStore : ITypeshiftStore
{
    private readonly ITypeshiftStore _store = new InMemoryTypeshiftStore();

    public int WholeLoads { get; private set; }

    public Task<Stored<TData>?> LoadAsync<TData>(Guid id, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        _store.LoadAsync<TData>(id, ct);

    public Task<long> SaveAsync<TData>(Guid id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData> =>
        _store.SaveAsync(id, data, revision, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        WholeLoads++;
        return _store.LoadConversationAsync(conversation, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        _store.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
