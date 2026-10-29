namespace Typeshift.Tests.Conversations.Support;

// Loads like a store in memory and fails every append, as a store that went down mid-run
public sealed class FailingAppendStore : IConversationStore
{
    private readonly InMemoryTypeshiftStore _store = new();

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        throw new IOException("The store is down.");
}
