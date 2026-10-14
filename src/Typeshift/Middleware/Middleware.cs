using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Middleware around every run, streaming or not; registered with <c>WithRunMiddleware&lt;T&gt;()</c>, resolved from
/// the run's services.
/// </summary>
public interface IRunMiddleware
{
    /// <summary>
    /// Wraps a run.
    /// </summary>
    /// <param name="run">The run; <see cref="AgentRun.Response"/> is set once <paramref name="next"/> returns.</param>
    /// <param name="next">Runs the rest of the pipeline.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>A task that completes with the run.</returns>
    Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct);
}

/// <summary>
/// Middleware around every tool call; registered with <c>WithToolMiddleware&lt;T&gt;()</c>, resolved from the run's
/// services.
/// </summary>
public interface IToolMiddleware
{
    /// <summary>
    /// Wraps a tool call.
    /// </summary>
    /// <param name="call">The call: the function, its arguments, the messages so far.</param>
    /// <param name="next">Calls the tool.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The result the model receives.</returns>
    ValueTask<object?> InvokeAsync(FunctionInvocationContext call, Func<ValueTask<object?>> next, CancellationToken ct);
}
