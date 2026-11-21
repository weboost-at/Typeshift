namespace Typeshift.Tests.Conversations.Support;

// Loads like a store in memory and fails every append, as a store that went down mid-run
public sealed class FailingAppendStore : IConversationStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

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
        throw new IOException("The store is down.");
}
