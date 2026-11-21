namespace Typeshift.Tests.Conversations.Support;

// A store in memory that holds the next load of a conversation until its gate opens, as a slow database would while
// another run of the conversation starts
public sealed class GatedLoadStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private int _holding;

    public TaskCompletionSource Loading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void HoldNextLoad() => Interlocked.Exchange(ref _holding, 1);

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        _store.LoadConversationAsync(conversation, cancellationToken);

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _holding, 0) == 1)
        {
            Loading.SetResult();
            await Gate.Task.WaitAsync(cancellationToken);
        }

        return await ((IConversationStore<Guid>)_store).LoadConversationSinceCompactionAsync(
            conversation,
            cancellationToken);
    }

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        _store.AppendConversationAsync(conversation, entry, cancellationToken);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        _store.LoadDecisionsAsync(conversation, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
}
