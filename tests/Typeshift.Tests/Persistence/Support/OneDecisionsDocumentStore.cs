namespace Typeshift.Tests.Persistence.Support;

// Keeps one decisions document whatever the conversation, as a store whose query forgot the conversation id
public sealed class OneDecisionsDocumentStore : IDecisionStore<Guid>
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadDecisionsAsync(Guid conversation, CancellationToken cancellationToken) =>
        _store.LoadDecisionsAsync(Guid.Empty, cancellationToken);

    public Task<long> SaveDecisionsAsync(
        Guid conversation,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveDecisionsAsync(Guid.Empty, json, loadedVersion, cancellationToken);
}
