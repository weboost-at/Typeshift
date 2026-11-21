namespace Typeshift.Tests.Persistence.Support;

// Saves a conversation's decisions over whatever is stored, ignoring the version it is given
public sealed class NoConflictDecisionStore : IDecisionStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadDecisionsAsync(conversation, ct);

    public async Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct)
    {
        var stored = await _store.LoadDecisionsAsync(conversation, ct);
        return await _store.SaveDecisionsAsync(conversation, json, stored?.Version ?? 0, ct);
    }
}
