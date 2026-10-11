using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// Where a chat runs, carried in <see cref="ChatOptions.AdditionalProperties"/>: an agent's unique name.
/// </summary>
public sealed record ChatExecutionContext(string Value)
{
    /// <summary>
    /// The context of a chat that runs outside any agent.
    /// </summary>
    public static ChatExecutionContext Unknown { get; } = new("Unknown");

    internal const string Key = "chat-execution-context";
}
