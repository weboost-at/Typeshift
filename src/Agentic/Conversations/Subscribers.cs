namespace Agentic.Conversations;

internal static class Subscribers
{
    // A subscriber gets a token cancelled at the limit, and the chat call waits for that token whatever the
    // subscriber does; what a subscriber throws stays with it
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
