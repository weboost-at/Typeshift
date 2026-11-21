using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Middleware around every run, streaming or not; registered with <c>WithRunMiddleware&lt;T&gt;()</c>, resolved from
/// the run's services.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/middleware#wrap-every-run">Wrap every run</see>.
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

    /// <summary>
    /// Sees input steered into a run, with <c>ConversationTracker.TrySteer</c>, before the run takes it: no model
    /// has received it, and the conversation has not stored it. Return the input to let it pass, another message
    /// to take in its place, or null to drop it. Unless implemented, the input passes.
    /// </summary>
    /// <param name="run">The run the input was steered into.</param>
    /// <param name="input">The input, a user message.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>
    /// The message the run takes, which the model receives and the conversation stores; null when it takes none.
    /// </returns>
    ValueTask<ChatMessage?> OnSteeringAsync(AgentRun run, ChatMessage input, CancellationToken ct) => new(input);
}

/// <summary>
/// Middleware around every tool call; registered with <c>WithToolMiddleware&lt;T&gt;()</c>, resolved from the run's
/// services.
/// More in the guide:
/// <see href="https://weboost-at.github.io/Typeshift/guide/middleware#wrap-every-tool-call">Wrap every tool call</see>.
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
