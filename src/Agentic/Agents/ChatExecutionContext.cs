using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// Where a chat runs, carried in <see cref="ChatOptions.AdditionalProperties"/>: the name of the agent that runs it.
/// </summary>
/// <param name="Value">The agent's name.</param>
public sealed record ChatExecutionContext(string Value)
{
    /// <summary>
    /// The context of a chat that runs outside any agent.
    /// </summary>
    public static ChatExecutionContext Unknown { get; } = new("Unknown");

    internal const string Key = "chat-execution-context";
}
