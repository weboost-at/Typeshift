namespace Typeshift.Tests.Persistence.Support;

// A store of the app's own making that counts how often something disposed it
public sealed class DisposalCountingStore(IAgentSettingsStore inner) : IAgentSettingsStore, IDisposable
{
    public int Disposed { get; private set; }

    public Task<StoredDocument?> LoadAsync(string kind, CancellationToken ct) => inner.LoadAsync(kind, ct);

    public Task<long> SaveAsync(string kind, string json, long loadedVersion, CancellationToken ct) =>
        inner.SaveAsync(kind, json, loadedVersion, ct);

    public void Dispose() => Disposed++;
}
