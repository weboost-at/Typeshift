namespace Typeshift.Tests.Conversations.Support;

// A store in memory whose first load of a conversation waits for a gate, as a slow database would while another run
// of the conversation starts
public sealed class GatedLoadStore : ITypeshiftStore
{
    private readonly InMemoryTypeshiftStore _store = new();
    private int _loads;

    public TaskCompletionSource Loading { get; } = new();

    public TaskCompletionSource Gate { get; } = new();

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadConversationAsync(conversation, ct);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        if (Interlocked.Increment(ref _loads) == 1)
        {
            Loading.SetResult();
            await Gate.Task.WaitAsync(ct);
        }

        return await ((IConversationStore)_store).LoadConversationSinceCompactionAsync(conversation, ct);
    }

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        _store.AppendConversationAsync(conversation, entry, ct);
}
