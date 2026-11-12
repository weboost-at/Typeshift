namespace Typeshift.Tests.Hosting.Support;

// A singleton that records whether the container disposed it
public sealed class DisposableStore : ISettingsStore, IDisposable
{
    private readonly InMemoryTypeshiftStore _store = new();

    public bool Disposed { get; private set; }

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken ct) =>
        _store.LoadAsync(id, kind, ct);

    public Task<long> SaveAsync(Guid id, string kind, string json, long loadedVersion, CancellationToken ct) =>
        _store.SaveAsync(id, kind, json, loadedVersion, ct);

    public void Dispose() => Disposed = true;
}
