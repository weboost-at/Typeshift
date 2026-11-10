using System.Collections.Concurrent;

namespace Typeshift;

/// <summary>
/// A store in memory, for tests and samples. Each project sees it as <c>InMemoryTypeshiftStore</c>. It keeps each
/// document as its JSON, so a load returns a copy and a round trip is tested on every save.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId>, IAgentSettingsStore where TId : notnull
{
    private readonly ConcurrentDictionary<(TId Id, string Kind), StoredDocument> _documents = [];

    private readonly ConcurrentDictionary<string, StoredDocument> _agentDocuments = [];

    private readonly ConcurrentDictionary<TId, List<string>> _conversations = [];

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
    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct)
    {
        if (!_conversations.TryGetValue(conversation, out var entries))
            return Task.FromResult<IReadOnlyList<ConversationEntry>>([]);

        lock (entries)
        {
            return Task.FromResult<IReadOnlyList<ConversationEntry>>(
                [.. entries.Select(ConversationEntry.Deserialize)]);
        }
    }

    /// <inheritdoc />
    public Task AppendConversationAsync(TId conversation, ConversationEntry entry, CancellationToken ct)
    {
        var entries = _conversations.GetOrAdd(conversation, _ => []);

        lock (entries)
        {
            entries.Add(entry.Serialize());
        }

        return Task.CompletedTask;
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
}
