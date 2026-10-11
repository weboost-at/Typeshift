using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// What a single run changes; the next run starts clean.
/// </summary>
public sealed record RunOverrides
{
    /// <summary>
    /// A run that changes nothing.
    /// </summary>
    public static RunOverrides None { get; } = new();

    /// <summary>
    /// Chat options for this run: allowlisted values apply, and its conversation id always does.
    /// </summary>
    public ChatOptions? ChatOptions { get; init; }

    /// <summary>
    /// Tool settings for this run, applied to the tools reading their type, allowlisted members only.
    /// </summary>
    public IReadOnlyList<object> ToolSettings { get; init; } = [];
}
