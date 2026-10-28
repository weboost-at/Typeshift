namespace Typeshift.Tests.Conversations.Support;

// Loads like a store in memory and fails every append, as a store that went down mid-run
public sealed class FailingAppendStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(string kind, Guid id, CancellationToken ct) =>
        _store.LoadAsync(kind, id, ct);

    public Task<long> SaveAsync(string kind, Guid id, string json, int version, long revision, CancellationToken ct) =>
        _store.SaveAsync(kind, id, json, version, revision, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        throw new IOException("The store is down.");
}
