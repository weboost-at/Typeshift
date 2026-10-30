namespace Typeshift.Tests.Persistence.Support;

// Returns a conversation newest first, as a store that sorts by a descending key would; its settings are right
public sealed class NewestFirstStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = await _store.LoadConversationAsync(conversation, ct);
        return [.. entries.Reverse()];
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
