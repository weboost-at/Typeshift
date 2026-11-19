using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// The context of a caller such as a UI thread: it counts what is posted to it and runs that on the pool as itself.
// A run starts on it and is awaited as by a caller that leaves the context itself, so each post is one the run made
public sealed class CallerContext : SynchronizationContext
{
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _posts;

    public int Posts => Volatile.Read(ref _posts);

    // Completes once the run's first step returned to the caller, so the run waits for what its step awaited
    public Task Waiting => _waiting.Task;

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
            var updates = run().ConfigureAwait(false).GetAsyncEnumerator();
            var count = 0;

            try
            {
                var next = updates.MoveNextAsync();
                _waiting.TrySetResult();

                while (await next)
                {
                    count++;
                    next = updates.MoveNextAsync();
                }
            }
            finally
            {
                await updates.DisposeAsync();
            }

            return count;
        });

    public Task<T> RunAsync<T>(Func<Task<T>> run) =>
        Task.Run(async () =>
        {
            SetSynchronizationContext(this);
            var running = run();
            _waiting.TrySetResult();

            return await running.ConfigureAwait(false);
        });
}
