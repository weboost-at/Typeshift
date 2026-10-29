namespace Typeshift.Tests.Persistence.Support;

// Returns a conversation newest first, as a store that sorts by a descending key would
public sealed class NewestFirstStore : IConversationStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        var entries = await _store.LoadConversationAsync(conversation, ct);
        return [.. entries.Reverse()];
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
