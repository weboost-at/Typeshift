namespace Typeshift.Tests.Persistence.Support;

// A store of the app's own making that counts how often something disposed it
public sealed class DisposalCountingStore(IAgentSettingsStore inner) : IAgentSettingsStore, IDisposable
{
    public int Disposed { get; private set; }

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken cancellationToken) =>
        inner.LoadAsync(kind, cancellationToken);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken cancellationToken) =>
        inner.SaveAsync(kind, json, loadedVersion, cancellationToken);

    public void Dispose() => Disposed++;
}
