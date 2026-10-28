namespace Typeshift.Tests.Conversations.Support;

// A store in memory that counts the whole conversations it loaded, as a store whose conversations grow long would
public sealed class CountingLoadStore : ITypeshiftStore
{
    private readonly ITypeshiftStore _store = new InMemoryTypeshiftStore();

    public int WholeLoads { get; private set; }

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, int version, long revision, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, version, revision, ct);

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
