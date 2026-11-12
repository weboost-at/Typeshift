namespace Typeshift.Tests.Hosting.Support;

// Disposes only asynchronously, so a synchronous dispose of the scope that created it throws
public sealed class AsyncOnlyMiddleware : IRunMiddleware, IAsyncDisposable
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) => next();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
