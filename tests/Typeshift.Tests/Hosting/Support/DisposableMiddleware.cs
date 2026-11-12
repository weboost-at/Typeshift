namespace Typeshift.Tests.Hosting.Support;

// Scoped, as the chain registers run middleware: each scope's instance records itself, so a test sees whether that
// scope disposed it
public sealed class DisposableMiddleware : IRunMiddleware, IDisposable
{
    public DisposableMiddleware(CreatedMiddleware created) => created.Add(this);

    public int Disposals { get; private set; }

    public bool Disposed => Disposals > 0;

    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct) => next();

    public void Dispose() => Disposals++;
}

public sealed class CreatedMiddleware : List<DisposableMiddleware>;
