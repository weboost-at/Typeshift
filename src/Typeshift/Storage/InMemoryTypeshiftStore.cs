using System.Collections.Concurrent;

namespace Typeshift;

/// <summary>
/// A store in memory, for tests and samples. Each project sees it as <c>InMemoryTypeshiftStore</c>. It keeps each
/// document as its JSON, so a load returns a copy and a round trip is tested on every save.
/// </summary>
/// <typeparam name="TId">The id type.</typeparam>
public sealed class InMemoryTypeshiftStore<TId> : ITypeshiftStore<TId> where TId : notnull
{
    private readonly ConcurrentDictionary<(TId Id, string Kind), StoredDocument> _documents = [];

    private readonly ConcurrentDictionary<TId, List<string>> _conversations = [];

    /// <inheritdoc />
    public Task<StoredDocument?> LoadAsync(TId id, string kind, CancellationToken ct) =>
        Task.FromResult(_documents.GetValueOrDefault((id, kind)));

    /// <inheritdoc />
    public Task<long> SaveAsync(TId id, string kind, string json, int version, long revision, CancellationToken ct)
    {
        var key = (id, kind);
        var saved = new StoredDocument(json, version, revision + 1);

        var won = revision == 0
            ? _documents.TryAdd(key, saved)
            : _documents.TryGetValue(key, out var stored)
                && stored.Revision == revision
                && _documents.TryUpdate(key, saved, stored);

        return won
            ? Task.FromResult(saved.Revision)
            : throw new StoreConflictException($"The {kind} of {id} was saved since revision {revision}.");
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(TId conversation, CancellationToken ct)
    {
        if (!_conversations.TryGetValue(conversation, out var entries))
            return Task.FromResult<IReadOnlyList<ConversationEntry>>([]);

        lock (entries)
        {
            return Task.FromResult<IReadOnlyList<ConversationEntry>>(
                [.. entries.Select(json => ConversationEntry.Deserialize(json, ConversationEntry.Version))]);
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
}
