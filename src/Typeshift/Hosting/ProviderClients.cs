using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Typeshift;

// D88: the clients of the provider documents runs are handed, one per equal document, a singleton of the runtime.
// Runs that hand in equal documents at the same time share one call of the connector. A run holds its client until its
// pipeline is disposed; a client no run holds, unused for the idle time, is disposed when a later run takes a client,
// which looks at most once a minute, so no timer runs. Disposing the runtime disposes the rest
internal sealed class ProviderClients(TimeProvider time, TimeSpan idle) : IDisposable
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly Lock _lock = new();
    private readonly Dictionary<ChatProvider, Entry> _entries = [];
    private long _swept = time.GetTimestamp();
    private bool _disposed;

    // The client of a document, and the run's hold on it, which the caller disposes when the run ends
    public async ValueTask<(IChatClient Client, IDisposable Hold)> GetAsync(
        ChatProvider provider,
        IServiceProvider services,
        string what,
        CancellationToken ct)
    {
        while (true)
        {
            var (entry, connects, unused) = Take(provider);
            Dispose(unused);

            try
            {
                if (connects) await ConnectAsync(entry, provider, services, what, ct).ConfigureAwait(false);
                return (await entry.Client.Task.WaitAsync(ct).ConfigureAwait(false), new Hold(this, entry));
            }
            catch (OperationCanceledException) when (!connects && entry.Client.Task.IsCanceled
                && !ct.IsCancellationRequested)
            {
                // The run that connected was cancelled, this one was not: it connects again
                Release(entry);
            }
            catch
            {
                Release(entry);
                throw;
            }
        }
    }

    public void Dispose()
    {
        List<Entry> entries;

        lock (_lock)
        {
            _disposed = true;
            entries = [.. _entries.Values];
            _entries.Clear();
        }

        Dispose(entries
            .Where(entry => entry.Client.Task.IsCompletedSuccessfully)
            .Select(entry => entry.Client.Task.Result));
    }

    private (Entry Entry, bool Connects, List<IChatClient>? Unused) Take(ChatProvider provider)
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var unused = Sweep();
            var connects = !_entries.TryGetValue(provider, out var entry);
            if (connects) _entries[provider] = entry = new Entry(time.GetTimestamp());

            entry!.Holders++;
            return (entry, connects, unused);
        }
    }

    private async Task ConnectAsync(
        Entry entry,
        ChatProvider provider,
        IServiceProvider services,
        string what,
        CancellationToken ct)
    {
        try
        {
            var connector = services.GetService<IChatProviderConnector>()
                ?? throw new InvalidOperationException(
                    $"No chat client serves {what} on the provider '{provider.Name ?? provider.Kind}', a document the "
                    + "run was handed, since no IChatProviderConnector is registered: register the class that turns "
                    + "your documents into clients with AddTypeshift().WithProviderConnector<TConnector>().");

            var client = await connector.ConnectAsync(provider, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"The IChatProviderConnector returned no client for the provider "
                    + $"'{provider.Name ?? provider.Kind}'.");

            if (!Keep(entry)) client.Dispose();
            entry.Client.TrySetResult(client);
        }
        catch (Exception exception)
        {
            Forget(entry);

            if (exception is OperationCanceledException && ct.IsCancellationRequested) entry.Client.TrySetCanceled(ct);
            else entry.Client.TrySetException(exception);

            throw;
        }
    }

    // A client that connected after the runtime was disposed is disposed at once, and the run fails
    private bool Keep(Entry entry)
    {
        lock (_lock)
        {
            if (!_disposed) return true;
        }

        entry.Client.TrySetException(new ObjectDisposedException(nameof(ProviderClients)));
        return false;
    }

    // A connection that failed is no client, so the next run with the document connects again
    private void Forget(Entry entry)
    {
        lock (_lock)
        {
            foreach (var (provider, kept) in _entries)
            {
                if (kept != entry) continue;

                _entries.Remove(provider);
                break;
            }
        }
    }

    private void Release(Entry entry)
    {
        lock (_lock)
        {
            entry.Holders--;
            entry.LastUsed = time.GetTimestamp();
        }
    }

    // The clients no run held for the idle time, taken out under the lock and disposed outside it
    private List<IChatClient>? Sweep()
    {
        if (time.GetElapsedTime(_swept) < SweepInterval) return null;

        _swept = time.GetTimestamp();

        var unused = _entries
            .Where(each => each.Value is { Holders: 0, Client.Task.IsCompletedSuccessfully: true }
                && time.GetElapsedTime(each.Value.LastUsed) >= idle)
            .ToList();

        foreach (var (provider, _) in unused)
        {
            _entries.Remove(provider);
        }

        return [.. unused.Select(each => each.Value.Client.Task.Result)];
    }

    private static void Dispose(IEnumerable<IChatClient>? clients)
    {
        foreach (var client in clients ?? [])
        {
            client.Dispose();
        }
    }

    private sealed class Entry(long created)
    {
        public TaskCompletionSource<IChatClient> Client { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Holders { get; set; }

        public long LastUsed { get; set; } = created;
    }

    // A run's hold, released once, when the run's pipeline is disposed
    private sealed class Hold(ProviderClients clients, Entry entry) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) clients.Release(entry);
        }
    }
}
