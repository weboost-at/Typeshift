namespace Typeshift.Tests.Decisions.Support;

// A store in memory that goes wrong on request. A save of decisions can lose a race: what another writer does runs
// right before it, straight on the store inside, as a second process would write between this one's load and its
// save. A save of decisions or an append can fail by its number, as a database that timed out once would
public sealed class UnreliableStore : ITypeshiftStore<Guid>
{
    private readonly Queue<Func<Task>> _others = new();
    private int _appends;

    // The store both writers share, which the other writer uses without the race
    public InMemoryTypeshiftStore<Guid> Inner { get; } = new();

    // The saves of decisions this store was asked for, lost and failed ones included
    public int Saves { get; private set; }

    // Every save loses to a writer that saved the document again
    public bool LosesEverySave { get; set; }

    // The number of the save of decisions that fails, the first being 1
    public int FailingSave { get; set; }

    // The number of the append that fails, the first being 1
    public int FailingAppend { get; set; }

    public void BeforeNextSave(Func<Task> other) => _others.Enqueue(other);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        Inner.LoadDecisionsAsync(conversation, cancellationToken);

    public async Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken)
    {
        if (++Saves == FailingSave) throw new IOException("The store timed out.");

        if (_others.TryDequeue(out var other)) await other();
        if (LosesEverySave) await SaveAgainAsync(conversation, cancellationToken);

        return await Inner.SaveDecisionsAsync(conversation, json, loadedVersion, cancellationToken);
    }

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        Inner.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        Inner.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        Inner.LoadConversationAsync(conversation, cancellationToken);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken cancellationToken) =>
        Inner.LoadConversationSinceCompactionAsync(conversation, cancellationToken);

    public Task AppendConversationAsync(
        Guid conversation,
        ConversationEntry entry,
        CancellationToken cancellationToken) =>
        Interlocked.Increment(ref _appends) == FailingAppend
            ? throw new IOException("The store timed out.")
            : Inner.AppendConversationAsync(conversation, entry, cancellationToken);

    private async Task SaveAgainAsync(Guid conversation, CancellationToken ct)
    {
        var stored = await Inner.LoadDecisionsAsync(conversation, ct);
        await Inner.SaveDecisionsAsync(conversation, stored?.Json ?? "{}", stored?.Version ?? 0, ct);
    }
}
