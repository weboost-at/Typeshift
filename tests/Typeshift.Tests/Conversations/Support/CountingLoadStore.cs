namespace Typeshift.Tests.Conversations.Support;

// A store in memory that counts the whole conversations it loaded, as a store whose conversations grow long would
public sealed class CountingLoadStore : IConversationStore
{
    private readonly IConversationStore _store = new InMemoryTypeshiftStore();

    public int WholeLoads { get; private set; }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        WholeLoads++;
        return _store.LoadConversationAsync(conversation, ct);
    }

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        _store.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
