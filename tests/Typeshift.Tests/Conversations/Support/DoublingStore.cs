namespace Typeshift.Tests.Conversations.Support;

// A store in memory that appends every entry twice, as a store from before every store kept a key on the entries'
// numbers stored a conversation when its append was retried after it had succeeded
public sealed class DoublingStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private readonly StoredJsonStore _conversations = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _conversations.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _conversations.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken cancellationToken)
    {
        _conversations.Add(conversation, entry.Serialize());
        _conversations.Add(conversation, entry.Serialize());

        return Task.CompletedTask;
    }

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        _store.LoadDecisionsAsync(conversation, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
}
