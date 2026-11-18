using System.Collections.Concurrent;

namespace Typeshift;

/// <summary>
/// A store in memory, for tests and samples, under ids of <typeparamref name="TId"/>. It keeps each document as its
/// JSON, so a load returns a copy and a round trip is tested on every save.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId>, IAgentSettingsStore where TId : notnull
{
    private readonly ConcurrentDictionary<(TId Id, string Kind), StoredDocument> _documents = [];

    private readonly ConcurrentDictionary<string, StoredDocument> _agentDocuments = [];

    private readonly ConcurrentDictionary<TId, Entries> _conversations = [];

    private readonly ConcurrentDictionary<TId, StoredDocument> _decisions = [];

    /// <inheritdoc />
    public Task<StoredDocument?> LoadAsync(TId id, string kind, CancellationToken ct) =>
        Task.FromResult(_documents.GetValueOrDefault((id, kind)));

    /// <inheritdoc />
    public Task<long> SaveAsync(TId id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        Save(_documents, (id, kind), json, loadedVersion, $"The {kind} of {id}");

    /// <inheritdoc />
    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) =>
        Task.FromResult(_agentDocuments.GetValueOrDefault(kind));

    /// <inheritdoc />
    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
        Save(_agentDocuments, kind, json, loadedVersion, $"The agent's {kind}");

    /// <inheritdoc />
    public bool RefusesTakenSequences => true;

    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct) =>
        Task.FromResult(Load(conversation, sinceSummary: false));

    // D71: from the last summary on, as its index was kept when it was appended, so the rest is not read
    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        TId conversation,
        CancellationToken ct) =>
        Task.FromResult(Load(conversation, sinceSummary: true));

    /// <inheritdoc />
    public Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct)
    {
        var entries = _conversations.GetOrAdd(conversation, _ => new());

        lock (entries)
        {
            // As a unique key on the conversation and the number would; an entry stored before entries were
            // numbered has none
            if (entry.Sequence > 0 && !entries.Sequences.Add(entry.Sequence))
            {
                throw new StoreConflictException(
                    $"Conversation {conversation} holds an entry numbered {entry.Sequence} already.");
            }

            if (entry.IsSummary) entries.LastSummary = entries.Json.Count;
            entries.Json.Add(entry.Serialize());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<StoredDocument?> LoadDecisionsAsync(TId conversation, CancellationToken ct) =>
        Task.FromResult(_decisions.GetValueOrDefault(conversation));

    /// <inheritdoc />
    public Task<long> SaveDecisionsAsync(TId conversation, string json, long loadedVersion, CancellationToken ct) =>
        Save(_decisions, conversation, json, loadedVersion, $"The decisions of {conversation}");

    private IReadOnlyList<ConversationEntry> Load(TId conversation, bool sinceSummary)
    {
        if (!_conversations.TryGetValue(conversation, out var entries)) return [];

        lock (entries)
        {
            var start = sinceSummary ? entries.LastSummary : 0;
            var loaded = new ConversationEntry[entries.Json.Count - start];

            for (var i = 0; i < loaded.Length; i++)
            {
                loaded[i] = ConversationEntry.Deserialize(entries.Json[start + i]);
            }

            return loaded;
        }
    }

    private static Task<long> Save<TKey>(
        ConcurrentDictionary<TKey, StoredDocument> documents,
        TKey key,
        string json,
        long loadedVersion,
        string what)
        where TKey : notnull
    {
        var saved = new StoredDocument(json, loadedVersion + 1);

        var won = loadedVersion == 0
            ? documents.TryAdd(key, saved)
            : documents.TryGetValue(key, out var stored)
                && stored.Version == loadedVersion
                && documents.TryUpdate(key, saved, stored);

        return won
            ? Task.FromResult(saved.Version)
            : throw new StoreConflictException($"{what} was saved since version {loadedVersion}.");
    }

    // A conversation's entries as their JSON, in the order appended, the numbers they hold and where the last summary
    // is, or 0 without one
    private sealed class Entries
    {
        public List<string> Json { get; } = [];

        public HashSet<long> Sequences { get; } = [];

        public int LastSummary { get; set; }
    }
}
