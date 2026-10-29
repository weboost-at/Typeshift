namespace Typeshift.Tests.Persistence.Support;

// Starts a conversation at its first summary, as a store that looks the summary up by the lowest key would
public sealed class FirstSummaryStore : IConversationStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        var entries = await _store.LoadConversationAsync(conversation, ct);
        return entries.Any(entry => entry.IsSummary) ? [.. entries.SkipWhile(entry => !entry.IsSummary)] : entries;
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
