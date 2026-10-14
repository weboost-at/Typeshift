using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Runs <typeparamref name="TAgent"/> with the services of the scope it was resolved from: its tools, context
/// providers, middleware and chat client provider all resolve there. Registered as transient by
/// <c>AddTypeshift()</c>. The generator adds the agent's typed members: <c>RunAsync</c>, <c>RunAsync&lt;T&gt;</c>,
/// <c>RunStreamingAsync</c>, <c>LoadSettingsAsync</c>, <c>SaveSettingsAsync</c>, <c>ConfigureAsync</c> and
/// <c>ResolveOptionsAsync</c>.
/// </summary>
/// <typeparam name="TAgent">The agent.</typeparam>
public sealed class AgentHost<TAgent> where TAgent : Agent
{
    internal AgentHost()
    {
    }

    /// <summary>
    /// The agent as its code declares it.
    /// </summary>
    public TAgent Agent => throw null!;

    /// <summary>
    /// The raw path: creates the client of a run whose options <c>ResolveOptionsAsync</c> resolved. It asks the
    /// <see cref="IChatClientProvider"/> for the options' model and wraps the client in conversation tracking, run
    /// middleware, context, compaction, telemetry and the tool loop.
    /// </summary>
    /// <param name="options">The resolved options; call the client with them.</param>
    /// <param name="ct">Cancels the creation.</param>
    /// <returns>The client.</returns>
    public Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default) => throw null!;
}

/// <summary>
/// Maps a model name to the client that serves it. Resolved from the run's services, so it may pick a customer's
/// own credentials.
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// Returns the client of a model. Typeshift never disposes it.
    /// </summary>
    ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct);
}

/// <summary>
/// One run on its way to the model, as run middleware and context providers see it.
/// </summary>
public sealed class AgentRun
{
    internal AgentRun()
    {
    }

    /// <summary>
    /// The agent that runs.
    /// </summary>
    public Agent Agent => throw null!;

    /// <summary>
    /// The messages the model receives: the resumed conversation and the input. Middleware may change them.
    /// </summary>
    public IList<ChatMessage> Messages => throw null!;

    /// <summary>
    /// The resolved options. Middleware may change them.
    /// </summary>
    public ChatOptions Options => throw null!;

    /// <summary>
    /// The run's services.
    /// </summary>
    public IServiceProvider Services => throw null!;

    /// <summary>
    /// The response, once the run has one; middleware that sets it without calling <c>next</c> answers in place of
    /// the model.
    /// </summary>
    public ChatResponse? Response { get; set; }
}

/// <summary>
/// Middleware around every run, streaming or not; registered with <c>WithRunMiddleware&lt;T&gt;()</c>.
/// </summary>
public interface IRunMiddleware
{
    /// <summary>
    /// Wraps a run.
    /// </summary>
    /// <param name="run">The run; <see cref="AgentRun.Response"/> is set once <paramref name="next"/> returns.</param>
    /// <param name="next">Runs the rest of the pipeline.</param>
    /// <param name="ct">Cancels the run.</param>
    Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct);
}

/// <summary>
/// Middleware around every tool call; registered with <c>WithToolMiddleware&lt;T&gt;()</c>.
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

/// <summary>
/// Adds context to every run of the agents that use it: memory, retrieved documents, the customer's profile.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Returns what to add to a run, after the conversation is resumed and before the model is called.
    /// </summary>
    ValueTask<AgentContext> GetContextAsync(AgentRun run, CancellationToken ct);
}

/// <summary>
/// What a context provider adds to a run; each part is appended to what the run already has.
/// </summary>
public sealed record AgentContext
{
    /// <summary>
    /// Appended to the agent's instructions.
    /// </summary>
    public string? Instructions { get; init; }

    /// <summary>
    /// Sent ahead of the input; never stored with the conversation.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];

    /// <summary>
    /// Offered for this run only.
    /// </summary>
    public IReadOnlyList<AITool> Tools { get; init; } = [];
}

/// <summary>
/// Connects the MCP servers agents name in <c>UseMcpServer</c>, with the MCP client of the host's choice.
/// </summary>
public interface IMcpToolProvider
{
    /// <summary>
    /// Returns the tools of a server.
    /// </summary>
    ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct);
}

/// <summary>
/// What runs return beyond Microsoft.Extensions.AI.
/// </summary>
public static class ChatResponseExtensions
{
    extension(ChatResponse response)
    {
        /// <summary>
        /// The tool calls waiting for the host's approval; answer each with <c>CreateResponse(approved)</c> in the
        /// next run of the conversation.
        /// </summary>
        public IReadOnlyList<ToolApprovalRequestContent> ApprovalRequests => throw null!;
    }
}
