namespace Typeshift.Tests.Conversations.Support;

// A store in memory that fails one append, by its number, as a database that timed out once would
public sealed class FlakyAppendStore : IConversationStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private int _appends;

    // The number of the append that fails, the first being 1
    public int Failing { get; set; }

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
        Interlocked.Increment(ref _appends) == Failing
            ? throw new IOException("The store timed out.")
            : _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
