using System.Collections.Concurrent;

namespace Typeshift.Tests.Conversations.Support;

// A store in memory that records each conversation it appends to, so a test sees every conversation a run started
public sealed class RecordingStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();
    private readonly ConcurrentQueue<Guid> _appended = new();

    // Each conversation once, in the order it was first stored
    public IReadOnlyList<Guid> Conversations => [.. _appended.Distinct()];

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        _store.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        _appended.Enqueue(conversation);
        return _store.AppendConversationAsync(conversation, entry, ct);
    }
}
