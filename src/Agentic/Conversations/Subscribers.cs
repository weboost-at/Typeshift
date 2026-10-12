namespace Agentic.Conversations;

internal static class Subscribers
{
    // Subscribers get a token cancelled at the limit and the call waits no longer; what they throw stays theirs
    public static async Task RaiseAsync(Func<CancellationToken, Task>? handlers, TimeSpan limit, TimeProvider time)
    {
        if (handlers is null) return;

        using var cancellation = new CancellationTokenSource(limit, time);

        var invocations = handlers
            .GetInvocationList()
            .Cast<Func<CancellationToken, Task>>()
            .Select(handler => InvokeAsync(handler, cancellation.Token));

        await Task.WhenAll(invocations)
            .WaitAsync(cancellation.Token)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    // A handler that throws before it returns a task faults its task instead
    private static async Task InvokeAsync(Func<CancellationToken, Task> handler, CancellationToken ct) =>
        await handler(ct);
}
