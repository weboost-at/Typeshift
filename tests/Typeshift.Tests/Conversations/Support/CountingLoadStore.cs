namespace Typeshift.Tests.Conversations.Support;

// A store in memory that counts the whole conversations it loaded, as a store whose conversations grow long would,
// and the loads from the last summary on, of which a run makes one
public sealed class CountingLoadStore : IConversationStore<Guid>
{
    private readonly IConversationStore<Guid> _store = new InMemoryTypeshiftStore<Guid>();

    public int WholeLoads { get; private set; }

    public int Loads { get; private set; }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        WholeLoads++;
        return _store.LoadConversationAsync(conversation, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        Loads++;
        return _store.LoadConversationSinceCompactionAsync(conversation, ct);
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
