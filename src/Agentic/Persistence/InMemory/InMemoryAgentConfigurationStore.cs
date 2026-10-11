using System.Collections.Concurrent;
using Agentic.Agents;

namespace Agentic.Persistence.InMemory;

/// <summary>
/// Keeps agent configurations in memory, for tests, samples and single-process hosts.
/// </summary>
public sealed class InMemoryAgentConfigurationStore : IAgentConfigurationStore
{
    private readonly ConcurrentDictionary<string, AgentConfiguration> _configurations = [];

    /// <inheritdoc />
    public Task<AgentConfiguration?> LoadAsync(string uniqueName, CancellationToken ct) =>
        Task.FromResult(_configurations.GetValueOrDefault(uniqueName));

    /// <inheritdoc />
    public Task SaveAsync(AgentConfiguration configuration, CancellationToken ct)
    {
        _configurations[configuration.UniqueName] = configuration;
        return Task.CompletedTask;
    }
}
