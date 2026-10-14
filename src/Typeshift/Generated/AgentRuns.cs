using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Typeshift.Generated;

/// <summary>
/// The runs behind an agent's generated host members.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class AgentRuns
{
    /// <summary>
    /// Runs the agent on one message.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the agent's code.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response.</returns>
    public static Task<ChatResponse> RunAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync(input, settings, ct);

    /// <summary>
    /// Runs the agent on one message, asking for JSON of <typeparamref name="TResult"/>'s schema.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <typeparam name="TResult">The result.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the agent's code.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The response, with the result.</returns>
    public static Task<ChatResponse<TResult>> RunAsync<TAgent, TResult>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunCoreAsync<TResult>(input, settings, ct);

    /// <summary>
    /// Runs the agent on one message, streaming the updates.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="input">The message.</param>
    /// <param name="settings">The run's settings, over the agent's code.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>The updates.</returns>
    public static IAsyncEnumerable<ChatResponseUpdate> RunStreamingAsync<TAgent>(
        AgentHost<TAgent> host,
        ChatMessage input,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.RunStreamingCoreAsync(input, settings, ct);

    /// <summary>
    /// Resolves the options of a run: the agent's code, then the run's settings, then its tools.
    /// </summary>
    /// <typeparam name="TAgent">The agent.</typeparam>
    /// <param name="host">The host, with the run's services.</param>
    /// <param name="settings">The run's settings, over the agent's code.</param>
    /// <param name="ct">Cancels the resolution.</param>
    /// <returns>The options, for <see cref="AgentHost{TAgent}.CreateChatClientAsync"/>.</returns>
    public static Task<ChatOptions> ResolveOptionsAsync<TAgent>(
        AgentHost<TAgent> host,
        object? settings,
        CancellationToken ct)
        where TAgent : Agent =>
        host.ResolveOptionsCoreAsync(settings, ct);
}
