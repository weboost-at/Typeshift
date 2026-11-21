namespace Typeshift.Tests.Persistence.Support;

// Keeps one document of agents' own settings whatever its kind, as a store that missed that each agent has its own;
// settings under an id and conversations work
public sealed class OneAgentDocumentStore : ITypeshiftStore<Guid>, IAgentSettingsStore
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync("settings", cancellationToken);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken cancellationToken) =>
        _store.SaveAsync("settings", json, loadedVersion, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        _store.LoadDecisionsAsync(conversation, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
}
