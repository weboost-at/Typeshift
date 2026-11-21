using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// The context of a caller such as a UI thread: it counts what is posted to it and runs that on the pool as itself.
// A run starts on it and is awaited as by a caller that leaves the context itself, so each post is one the run made
public sealed class CallerContext : SynchronizationContext
{
    private int _posts;

    public int Posts => Volatile.Read(ref _posts);

    public override void Post(SendOrPostCallback d, object? state)
    {
        Interlocked.Increment(ref _posts);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            SetSynchronizationContext(this);
            d(state);
        });
    }

    public Task<int> ReadAsync(Func<IAsyncEnumerable<ChatResponseUpdate>> run) =>
        Task.Run(async () =>
        {
            SetSynchronizationContext(this);
            var count = 0;

            await foreach (var _ in run().ConfigureAwait(false))
            {
                count++;
            }

            return count;
        });

    // Reads as a caller that stays on this context does, so it asks for each update from here, and leaves the stream
    // after the count of updates
    public Task StayAndReadAsync(Func<IAsyncEnumerable<ChatResponseUpdate>> run, int count) =>
        Task.Run(async () =>
        {
            SetSynchronizationContext(this);
            var read = 0;

            await foreach (var _ in run())
            {
                if (++read == count) break;
            }
        });

    public Task<T> RunAsync<T>(Func<Task<T>> run) =>
        Task.Run(async () =>
        {
            SetSynchronizationContext(this);
            return await run().ConfigureAwait(false);
        });
}
