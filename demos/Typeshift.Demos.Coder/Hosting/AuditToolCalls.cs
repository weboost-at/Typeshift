using Microsoft.Extensions.AI;
using Typeshift.Demos.Hosting;

namespace Typeshift.Demos.Coder.Hosting;

// Prints every tool call with its arguments and how long it took; a host would log or meter this
public sealed class AuditToolCalls(TimeProvider time) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        var arguments = call.CallContent.Arguments is { } sent
            ? Demo.Arguments(new FunctionCallContent(call.CallContent.CallId, call.Function.Name, sent))
            : "";

        var started = time.GetTimestamp();
        var result = await next();

        Console.Error.WriteLine(
            $"  [tool] {call.Function.Name}({arguments}) {time.GetElapsedTime(started).TotalMilliseconds:F0}ms");

        return result;
    }
}
