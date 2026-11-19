namespace Typeshift.Tests.Hosting.Support;

// A store in memory that answers once the test opens it, as a database answers later: a run's first call to it is
// what the run first waits for
public sealed class WaitingStore : ITypeshiftStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Open() => _opened.TrySetResult();

    public async Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.LoadAsync(id, kind, ct).ConfigureAwait(false);
    }

    public async Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.SaveAsync(id, kind, json, loadedVersion, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(Guid conversation, CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.LoadConversationAsync(conversation, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationSinceCompactionAsync(
        Guid conversation,
        CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.LoadConversationSinceCompactionAsync(conversation, ct).ConfigureAwait(false);
    }

    public async Task AppendConversationAsync(Guid conversation, ConversationEntry entry, CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        await _store.AppendConversationAsync(conversation, entry, ct).ConfigureAwait(false);
    }

    public async Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.LoadDecisionsAsync(conversation, ct).ConfigureAwait(false);
    }

    public async Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken ct)
    {
        await _opened.Task.WaitAsync(ct).ConfigureAwait(false);
        return await _store.SaveDecisionsAsync(conversation, json, loadedVersion, ct).ConfigureAwait(false);
    }
}
