namespace Typeshift;

/// <summary>
/// An agent another agent offers as a tool, named like it, with one <c>input</c> parameter.
/// </summary>
public sealed class AgentUse
{
    internal AgentUse(Func<IServiceProvider, IAgentHost> host) => Host = host;

    internal Func<IServiceProvider, IAgentHost> Host { get; }

    internal bool Background { get; private set; }

    internal SubAgentResult? Result { get; private set; }

    /// <summary>
    /// Starts the agent and returns at once, so the caller works on. Its answer reaches the calling run as a message
    /// before a later model call, and the run does not end while it works.
    /// </summary>
    /// <returns>The same use.</returns>
    public AgentUse InBackground()
    {
        Background = true;
        return this;
    }

    /// <summary>
    /// Asks the agent for JSON of <typeparamref name="TResult"/>'s schema, as <c>RunAsync&lt;TResult&gt;</c> does, and
    /// hands the caller's model that JSON as the call's result. An answer without it fails the call, saying why.
    /// </summary>
    /// <typeparam name="TResult">The type the agent answers in.</typeparam>
    /// <returns>The same use.</returns>
    public AgentUse WithResult<TResult>()
    {
        Result = new SubAgentResult<TResult>();
        return this;
    }
}
