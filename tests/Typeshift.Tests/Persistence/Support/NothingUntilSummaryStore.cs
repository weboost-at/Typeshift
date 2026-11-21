namespace Typeshift.Tests.Persistence.Support;

// Loads nothing for a run while the conversation has no summary, as a query from the last summary's row on would
// when it compares with a missing one
public sealed class NothingUntilSummaryStore : IConversationStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationAsync(conversation, cancellationToken);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        var entries = await _store.LoadConversationAsync(conversation, cancellationToken);
        return entries.Any(entry => entry.IsSummary) ? entries.SinceLastSummary() : [];
    }

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
