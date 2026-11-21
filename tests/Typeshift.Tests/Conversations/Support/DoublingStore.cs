namespace Typeshift.Tests.Conversations.Support;

// A store in memory that appends every entry twice, as a store without a key on the entries' numbers whose append
// was retried after it had succeeded
public sealed class DoublingStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private readonly StoredJsonStore _conversations = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _conversations.LoadConversationAsync(conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        _conversations.LoadConversationSinceCompactionAsync(conversation, ct);

    public async Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        await _conversations.AppendConversationAsync(conversation, entry, ct);
        await _conversations.AppendConversationAsync(conversation, entry, ct);
    }

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadDecisionsAsync(conversation, ct);

    public Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, ct);
}
