namespace Typeshift.Tests.Persistence.Support;

// Loads every entry without its run end, as a store that rebuilds entries from columns of its own and misses one
public sealed class DroppedMemberStore : IConversationStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken ct) =>
        WithoutRunEnds(await _store.LoadConversationAsync(conversation, ct));

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        WithoutRunEnds(await _store.LoadConversationSinceCompactionAsync(conversation, ct));

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);

    private static IReadOnlyList<ConversationEntry> WithoutRunEnds(IReadOnlyList<ConversationEntry> entries) =>
        [.. entries.Select(entry => entry with { EndsRun = false })];
}
