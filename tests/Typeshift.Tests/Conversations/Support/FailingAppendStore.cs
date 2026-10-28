namespace Typeshift.Tests.Conversations.Support;

// Loads like a store in memory and fails every append, as a store that went down mid-run
public sealed class FailingAppendStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, int version, long revision, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, version, revision, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        throw new IOException("The store is down.");
}
