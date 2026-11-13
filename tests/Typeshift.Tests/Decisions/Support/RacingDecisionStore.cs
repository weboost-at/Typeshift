namespace Typeshift.Tests.Decisions.Support;

// A store in memory whose saves of decisions lose a race on request: what another writer does runs right before the
// save, straight on the store inside, as a second process would write between this one's load and its save
public sealed class RacingDecisionStore : ITypeshiftStore<Guid>
{
    private readonly Queue<Func<Task>> _others = new();

    // The store both writers share, which the other writer uses without the race
    public InMemoryTypeshiftStore<Guid> Inner { get; } = new();

    // The saves of decisions this store was asked for, lost ones included
    public int Saves { get; private set; }

    // Every save loses to a writer that saved the document again
    public bool LosesEverySave { get; set; }

    public void BeforeNextSave(Func<Task> other) => _others.Enqueue(other);

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        Inner.LoadDecisionsAsync(conversation, ct);

    public async Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct)
    {
        Saves++;

        if (_others.TryDequeue(out var other)) await other();
        if (LosesEverySave) await SaveAgainAsync(conversation, ct);

        return await Inner.SaveDecisionsAsync(conversation, json, loadedVersion, ct);
    }

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        Inner.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        Inner.SaveAsync(id, kind, json, loadedVersion, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct) =>
        Inner.LoadConversationAsync(conversation, ct);

    public Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct) =>
        Inner.LoadConversationSinceCompactionAsync(conversation, ct);

    public Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct) =>
        Inner.AppendConversationAsync(conversation, entry, ct);

    private async Task SaveAgainAsync(Guid conversation, CancellationToken ct)
    {
        var stored = await Inner.LoadDecisionsAsync(conversation, ct);
        await Inner.SaveDecisionsAsync(conversation, stored?.Json ?? "{}", stored?.Version ?? 0, ct);
    }
}
