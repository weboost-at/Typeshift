namespace Typeshift.Tests.Persistence.Support;

// Inserts a decisions document that is not stored whatever version the save names, as an update that falls back to
// an insert would bring back the decisions of a conversation deleted meanwhile
public sealed class UpsertMissingDecisionStore : IDecisionStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken ct) =>
        _store.LoadDecisionsAsync(conversation, ct);

    public async Task<long> SaveDecisionsAsync(Guid conversation, string json, long loadedVersion, CancellationToken ct)
    {
        var stored = await _store.LoadDecisionsAsync(conversation, ct);
        return await _store.SaveDecisionsAsync(conversation, json, stored is null ? 0 : loadedVersion, ct);
    }
}
