namespace Typeshift.Tests.Conversations.Support;

// A store in memory whose first load of a conversation fails, as a database that dropped one connection would
public sealed class FailingLoadStore : IConversationStore<Guid>
{
    private readonly IConversationStore<Guid> _store = new InMemoryTypeshiftStore<Guid>();
    private int _loads;

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        Interlocked.Increment(ref _loads) == 1
            ? throw new IOException("The store dropped the connection.")
            : _store.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
