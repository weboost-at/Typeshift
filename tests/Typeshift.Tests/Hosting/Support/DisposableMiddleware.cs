namespace Typeshift.Tests.Hosting.Support;

// Scoped, as the chain registers run middleware: each scope's instance records itself, so a test sees whether that
// scope disposed it
public sealed class DisposableMiddleware : IRunMiddleware, IDisposable
{
    public DisposableMiddleware(CreatedMiddleware created) => created.Add(this);

    public bool Disposed { get; private set; }

    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) => next();

    public void Dispose() => Disposed = true;
}

public sealed class CreatedMiddleware : List<DisposableMiddleware>;
