namespace Typeshift.Samples.Stores;

// begin-snippet: minimal-store
internal sealed class MinimalStore : ITypeshiftStore<int>, IAgentSettingsStore
{
    // One lock around every call, so a save checks the version and writes in one step
    private readonly Lock _gate = new();

    private readonly Dictionary<(int Id, string Kind), StoredDocument> _settings = [];
    private readonly Dictionary<string, StoredDocument> _agentSettings = [];
    private readonly Dictionary<int, StoredDocument> _decisions = [];
    private readonly Dictionary<int, List<string>> _conversations = [];

    public Task<StoredDocument?> LoadAsync(int id, string kind, CancellationToken ct) =>
        Load(_settings, (id, kind));

    public Task<long> SaveAsync(int id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        Save(_settings, (id, kind), json, loadedVersion);

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) =>
        Load(_agentSettings, kind);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
        Save(_agentSettings, kind, json, loadedVersion);

    public Task<StoredDocument?> LoadDecisionsAsync(int conversation, CancellationToken ct) =>
        Load(_decisions, conversation);

    public Task<long> SaveDecisionsAsync(int conversation, string json, long loadedVersion, CancellationToken ct) =>
        Save(_decisions, conversation, json, loadedVersion);

    public Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct)
    {
        lock (_gate)
        {
            _conversations.TryAdd(conversation, []);
            _conversations[conversation].Add(entry.Serialize());
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(int conversation, CancellationToken ct)
    {
        lock (_gate)
        {
            IReadOnlyList<ConversationEntry> entries =
                [.. _conversations.GetValueOrDefault(conversation, []).Select(ConversationEntry.Deserialize)];

            return Task.FromResult(entries);
        }
    }

    // The whole conversation, cut at its last summary
    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        int conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    private Task<StoredDocument?> Load<TKey>(Dictionary<TKey, StoredDocument> documents, TKey key)
        where TKey : notnull
    {
        lock (_gate)
        {
            return Task.FromResult(documents.GetValueOrDefault(key));
        }
    }

    private Task<long> Save<TKey>(Dictionary<TKey, StoredDocument> documents, TKey key, string json, long loadedVersion)
        where TKey : notnull
    {
        lock (_gate)
        {
            // 0 when nothing is stored, so a first save inserts and any later one updates the version it loaded
            if ((documents.GetValueOrDefault(key)?.Version ?? 0) != loadedVersion)
                throw new StoreConflictException($"{key} was saved after version {loadedVersion}.");

            documents[key] = new StoredDocument(json, loadedVersion + 1);

            return Task.FromResult(loadedVersion + 1);
        }
    }
}
// end-snippet
