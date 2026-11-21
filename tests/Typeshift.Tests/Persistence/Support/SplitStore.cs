namespace Typeshift.Tests.Persistence.Support;

// Keeps settings and conversations under ids of one type through the two interfaces, not through ITypeshiftStore.
// Asked to, it loads a conversation newest first, so a check that left its conversations out would pass
public sealed class SplitStore(bool newestFirst = false) : ISettingsStore<Guid>, IConversationStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = await _store.LoadConversationAsync(conversation, ct);
        return newestFirst ? [.. entries.Reverse()] : entries;
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        (await LoadConversationAsync(conversation, ct)).SinceLastSummary();

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
