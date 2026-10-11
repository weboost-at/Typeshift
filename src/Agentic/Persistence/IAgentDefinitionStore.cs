using Agentic.StoredAgents;

namespace Agentic.Persistence;

/// <summary>
/// Stores agents defined as data; optional, register an implementation as a singleton to use stored agents.
/// </summary>
public interface IAgentDefinitionStore
{
    /// <summary>
    /// The stored agent with the unique name, or null.
    /// </summary>
    Task<StoredAgent?> LoadAsync(string uniqueName, CancellationToken ct);

    /// <summary>
    /// Every stored agent.
    /// </summary>
    Task<IReadOnlyList<StoredAgent>> ListAsync(CancellationToken ct);

    /// <summary>
    /// Stores the agent, replacing the one stored under the same unique name.
    /// </summary>
    Task SaveAsync(StoredAgent agent, CancellationToken ct);

    /// <summary>
    /// Removes the agent stored under the unique name.
    /// </summary>
    Task DeleteAsync(string uniqueName, CancellationToken ct);
}
