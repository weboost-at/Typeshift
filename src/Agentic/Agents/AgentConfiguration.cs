using System.Collections.Immutable;
using Agentic.Settings;
using Agentic.Tools;

namespace Agentic.Agents;

/// <summary>
/// The stored configuration of an agent, one document per unique name: its model and allowlisted overrides.
/// </summary>
public sealed record AgentConfiguration(string UniqueName)
{
    /// <summary>
    /// The model the agent runs on, as the registered <see cref="IChatClientProvider"/> knows it.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Stored chat options; only the agent's allowlisted options apply.
    /// </summary>
    public StoredSettings ChatOptions { get; init; } = StoredSettings.Empty;

    /// <summary>
    /// Stored tool settings by tool; only each tool's allowlisted members apply.
    /// </summary>
    public IReadOnlyDictionary<ToolName, StoredSettings> ToolSettings { get; init; } =
        ImmutableDictionary<ToolName, StoredSettings>.Empty;

    /// <summary>
    /// When the configuration was last stored.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
