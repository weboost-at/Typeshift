using Agentic.Agents;

namespace Agentic.Persistence;

/// <summary>
/// Stores agent configurations; optional, register an implementation as a singleton to persist them.
/// </summary>
public interface IAgentConfigurationStore
{
    /// <summary>
    /// The stored configuration of the agent, or null when none was stored.
    /// </summary>
    Task<AgentConfiguration?> LoadAsync(string uniqueName, CancellationToken ct);

    /// <summary>
    /// Stores the configuration, replacing the one stored under the same unique name.
    /// </summary>
    Task SaveAsync(AgentConfiguration configuration, CancellationToken ct);
}
