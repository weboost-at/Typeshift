using Microsoft.Extensions.AI;

namespace Typeshift;

// D30: the tool loop's function invoker, when middleware is registered: the first registered outermost
internal static class ToolMiddleware
{
    public static ValueTask<object?> InvokeAsync(
        IReadOnlyList<IToolMiddleware> middleware,
        int index,
        FunctionInvocationContext call,
        CancellationToken ct) =>
        index == middleware.Count
            ? call.Function.InvokeAsync(call.Arguments, ct)
            : middleware[index].InvokeAsync(call, () => InvokeAsync(middleware, index + 1, call, ct), ct);
}
