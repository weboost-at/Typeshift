namespace Typeshift.Tests.Conversations.Support;

// A store in memory that counts the whole conversations it loaded, as a store whose conversations grow long would,
// and the loads from the last summary on, of which a run makes one
public sealed class CountingLoadStore : IConversationStore<Guid>
{
    private readonly IConversationStore<Guid> _store = new InMemoryTypeshiftStore<Guid>();

    public int WholeLoads { get; private set; }

    public int Loads { get; private set; }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        WholeLoads++;
        return _store.LoadConversationAsync(conversation, cancellationToken);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        Loads++;
        return _store.LoadConversationSinceCompactionAsync(conversation, cancellationToken);
    }

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);
}
