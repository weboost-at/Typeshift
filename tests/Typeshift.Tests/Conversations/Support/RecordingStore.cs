using System.Collections.Concurrent;

namespace Typeshift.Tests.Conversations.Support;

// A store in memory that records each conversation it appends to, so a test sees every conversation a run started
public sealed class RecordingStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private readonly ConcurrentQueue<Guid> _appended = new();

    // Each conversation once, in the order it was first stored
    public IReadOnlyList<Guid> Conversations => [.. _appended.Distinct()];

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
        _store.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken cancellationToken)
    {
        _appended.Enqueue(conversation);
        return _store.AppendConversationAsync(conversation, entry, cancellationToken);
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
