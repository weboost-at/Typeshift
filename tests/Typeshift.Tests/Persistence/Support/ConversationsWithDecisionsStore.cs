namespace Typeshift.Tests.Persistence.Support;

// Keeps conversations and their decisions, where the guide says decisions belong, and no settings. Asked to, it
// saves decisions whatever version the save names, so a check that left its decisions out would pass
public sealed class ConversationsWithDecisionsStore(bool neverConflicts = false)
    : IConversationStore<Guid>, IDecisionStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    private readonly NoConflictDecisionStore _broken = new();

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        _store.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        neverConflicts ? _broken.LoadDecisionsAsync(conversation, ct) : _store.LoadDecisionsAsync(conversation, ct);

    public Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct) =>
        neverConflicts
            ? _broken.SaveDecisionsAsync(conversation, json, loadedVersion, ct)
            : _store.SaveDecisionsAsync(conversation, json, loadedVersion, ct);
}
