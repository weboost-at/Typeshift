namespace Typeshift.Tests.Persistence.Support;

// Keeps each agent's own settings in memory and nothing under an id, as the store of an app without settings ids
public sealed class AgentSettingsOnlyStore : IAgentSettingsStore
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(kind, cancellationToken);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken cancellationToken) =>
        _store.SaveAsync(kind, json, loadedVersion, cancellationToken);
}
