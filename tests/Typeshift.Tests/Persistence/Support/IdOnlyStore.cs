namespace Typeshift.Tests.Persistence.Support;

// Keys documents by id alone, as a store that missed that one id holds a document per agent; conversations work
public sealed class IdOnlyStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, "settings", ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, "settings", json, loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
