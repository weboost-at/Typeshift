namespace Typeshift.Tests.Persistence.Support;

// Returns a conversation newest first, as a store that sorts by a descending key would; its settings are right
public sealed class NewestFirstStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = await _store.LoadConversationAsync(conversation, ct);
        return [.. entries.Reverse()];
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadDecisionsAsync(conversation, ct);

    public Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, ct);
}
