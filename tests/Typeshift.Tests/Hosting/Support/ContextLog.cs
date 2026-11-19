using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Hosting.Support;

// The app's code a run called, and which of it ran where a synchronization context was current
public sealed class ContextLog
{
    private readonly ConcurrentQueue<(string Code, bool OnContext)> _calls = new();

    public IReadOnlyList<string> Calls => [.. _calls.Select(call => call.Code)];

    public IReadOnlyList<string> OnContext => [.. _calls.Where(call => call.OnContext).Select(call => call.Code)];

    public void Add(string code) => _calls.Enqueue((code, SynchronizationContext.Current is not null));
}

public sealed class ContextLoggingToolMiddleware(ContextLog log) : IToolMiddleware
{
    public const string Tool = "tool";

    public ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        log.Add(Tool);
        return next();
    }
}
