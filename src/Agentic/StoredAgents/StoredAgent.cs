using System.Collections.Immutable;
using Agentic.Settings;
using Agentic.Tools;

namespace Agentic.StoredAgents;

/// <summary>
/// An agent defined as data; it runs through the same path as an agent defined in code.
/// </summary>
public sealed record StoredAgent
{
    /// <summary>
    /// The unique name; a generated one never equals the name of a code agent.
    /// </summary>
    public required string UniqueName { get; init; }

    /// <summary>
    /// The name people see.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// What the agent does.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The chat options the agent runs with.
    /// </summary>
    public StoredAgentOptions Options { get; init; } = new();

    /// <summary>
    /// The tools the agent offers, in order.
    /// </summary>
    public IReadOnlyList<ToolName> Tools { get; init; } = [];

    /// <summary>
    /// The settings of its tools as JSON per member; they apply as a whole, like an agent's code settings.
    /// </summary>
    public IReadOnlyDictionary<ToolName, StoredSettings> ToolSettings { get; init; } =
        ImmutableDictionary<ToolName, StoredSettings>.Empty;

    /// <summary>
    /// A new stored agent under a generated unique name.
    /// </summary>
    public static StoredAgent Create(string displayName, TimeProvider time) =>
        new()
        {
            UniqueName = GenerateUniqueName(time),
            DisplayName = displayName
        };

    // The hyphen keeps it out of lower_snake_case, the only form a code agent's name takes
    private static string GenerateUniqueName(TimeProvider time) => $"stored-{Guid.CreateVersion7(time.GetUtcNow()):N}";
}
