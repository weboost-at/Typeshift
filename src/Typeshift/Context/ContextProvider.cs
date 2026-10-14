using Microsoft.Extensions.AI;

namespace Typeshift;

/// <summary>
/// Adds context to every run of the agents that use it: memory, retrieved documents, the customer's profile.
/// </summary>
public interface IContextProvider
{
    /// <summary>
    /// Returns what to add to a run, after the conversation is resumed and before the model is called.
    /// </summary>
    /// <param name="run">The run.</param>
    /// <param name="ct">Cancels the run.</param>
    /// <returns>What to add.</returns>
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
