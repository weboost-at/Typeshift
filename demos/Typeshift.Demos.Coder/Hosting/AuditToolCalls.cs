using System.Diagnostics;
using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder.Hosting;

// Prints every tool call with its arguments and how long it took; a host would log or meter this
public sealed class AuditToolCalls : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken ct)
    {
        var arguments = call.CallContent.Arguments is { } sent
            ? Demo.Arguments(new FunctionCallContent(call.CallContent.CallId, call.Function.Name, sent))
            : "";

        var started = Stopwatch.GetTimestamp();
        var result = await next();

        Console.Error.WriteLine(
            $"  [tool] {call.Function.Name}({arguments}) {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms");

        return result;
    }
}
