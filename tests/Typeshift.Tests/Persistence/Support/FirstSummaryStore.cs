namespace Typeshift.Tests.Persistence.Support;

// Starts a conversation at its first summary, as a store that looks the summary up by the lowest key would
public sealed class FirstSummaryStore : IConversationStore<Guid>
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
        return entries.Any(entry => entry.IsSummary) ? [.. entries.SkipWhile(entry => !entry.IsSummary)] : entries;
    }

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
