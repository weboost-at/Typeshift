namespace Typeshift.Tests.Persistence.Support;

// Keeps conversations and their decisions, where the guide says decisions belong, and no settings. Asked to, it
// saves decisions whatever version the save names, so a check that left its decisions out would pass
public sealed class ConversationsWithDecisionsStore(bool neverConflicts = false)
    : IConversationStore<Guid>, IDecisionStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    private readonly NoConflictDecisionStore _broken = new();

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        neverConflicts
            ? _broken.LoadDecisionsAsync(conversation, cancellationToken)
            : _store.LoadDecisionsAsync(conversation, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        neverConflicts
            ? _broken.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken)
            : _store.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
}
