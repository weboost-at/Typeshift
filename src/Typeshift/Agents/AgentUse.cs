namespace Typeshift;

/// <summary>
/// An agent another agent offers as a tool, named like it, with one <c>input</c> parameter.
/// </summary>
public sealed class AgentUse
{
    internal AgentUse(Func<IServiceProvider, IAgentHost> host) => Host = host;

    internal Func<IServiceProvider, IAgentHost> Host { get; }

    internal bool Background { get; private set; }

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
}
