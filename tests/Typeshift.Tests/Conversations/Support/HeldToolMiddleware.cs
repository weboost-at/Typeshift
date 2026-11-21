using Typeshift.Tests.Decisions.Support;
using Microsoft.Extensions.AI;

namespace Typeshift.Tests.Conversations.Support;

// Holds every tool call until released, so another instance of the app can run the conversation meanwhile
public sealed class HeldToolMiddleware(ToolHold hold) : IToolMiddleware
{
    public async ValueTask<object?> InvokeAsync(
        FunctionInvocationContext call,
        Func<ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        hold.Running.TrySetResult();
        await hold.Release.Task.WaitAsync(cancellationToken);

        return await next();
    }
}
