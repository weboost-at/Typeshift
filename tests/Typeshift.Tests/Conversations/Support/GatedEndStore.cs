namespace Typeshift.Tests.Conversations.Support;

// A store in memory that holds the first append of an entry that ends a run until its gate opens, as a slow
// database would while input arrives for a run that is ending. A test may name another entry to hold
public sealed class GatedEndStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private int _held;

    public Func<ConversationEntry, bool> Holds { get; set; } = entry => entry.EndsRun;

    public TaskCompletionSource Ending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        ((IConversationStore<Guid>)_store).LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public async Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken)
    {
        if (Holds(entry) && Interlocked.Exchange(ref _held, 1) == 0)
        {
            Ending.SetResult();
            await Gate.Task;
        }

        await _store.AppendConversationAsync(conversation, entry, cancellationToken);
    }

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        _store.LoadDecisionsAsync(conversation, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
}
