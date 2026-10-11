using System.Collections.Concurrent;
using Agentic.StoredAgents;

namespace Agentic.Persistence.InMemory;

/// <summary>
/// Keeps stored agents in memory, for tests, samples and single-process hosts.
/// </summary>
public sealed class InMemoryAgentDefinitionStore : IAgentDefinitionStore
{
    private readonly ConcurrentDictionary<string, StoredAgent> _agents = [];

    /// <inheritdoc />
    public Task<StoredAgent?> LoadAsync(string uniqueName, CancellationToken ct) =>
        Task.FromResult(_agents.GetValueOrDefault(uniqueName));

    /// <inheritdoc />
    public Task<IReadOnlyList<StoredAgent>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<StoredAgent>>([.. _agents.Values.OrderBy(agent => agent.UniqueName)]);

    /// <inheritdoc />
    public Task SaveAsync(StoredAgent agent, CancellationToken ct)
    {
        _agents[agent.UniqueName] = agent;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string uniqueName, CancellationToken ct)
    {
        _agents.TryRemove(uniqueName, out _);
        return Task.CompletedTask;
    }
}
