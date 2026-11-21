namespace Typeshift.Tests.Conversations.Support;

// A store in memory whose first load of a conversation fails, as a database that dropped one connection would
public sealed class FailingLoadStore : IConversationStore<Guid>
{
    private readonly IConversationStore<Guid> _store = new InMemoryTypeshiftStore<Guid>();
    private int _loads;

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        Interlocked.Increment(ref _loads) == 1
            ? throw new IOException("The store dropped the connection.")
            : _store.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
