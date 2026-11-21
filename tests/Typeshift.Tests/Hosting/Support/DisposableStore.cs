namespace Typeshift.Tests.Hosting.Support;

// A singleton that records whether the container disposed it
public sealed class DisposableStore : ISettingsStore<Guid>, IDisposable
{
    private readonly InMemoryTypeshiftStore<Guid> _store = new();

    public bool Disposed { get; private set; }

    public Task<StoredDocument?> LoadAsync(Guid id, string kind, CancellationToken cancellationToken) =>
        _store.LoadAsync(id, kind, cancellationToken);

    public Task<long> SaveAsync(
        Guid id,
        string kind,
        string json,
        long loadedVersion,
        CancellationToken cancellationToken) =>
        _store.SaveAsync(id, kind, json, loadedVersion, cancellationToken);

    public void Dispose() => Disposed = true;
}
