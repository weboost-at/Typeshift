namespace Typeshift.Tests.Persistence.Support;

// Keeps settings and conversations under ids of one type through the two interfaces, not through ITypeshiftStore.
// Asked to, it loads a conversation newest first, so a check that left its conversations out would pass
public sealed class SplitStore(bool newestFirst = false) : ISettingsStore<Guid>, IConversationStore<Guid>
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

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        var entries = await _store.LoadConversationAsync(conversation, cancellationToken);
        return newestFirst ? [.. entries.Reverse()] : entries;
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        (await LoadConversationAsync(conversation, cancellationToken)).SinceLastSummary();

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
