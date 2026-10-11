using System.Collections.Frozen;
using Agentic.Agents;
using Agentic.Persistence;
using Agentic.StoredAgents;

namespace Agentic.Catalogs;

/// <summary>
/// Every agent the host can see, defined in code or stored as data, by unique name.
/// </summary>
public sealed class AgentCatalog
{
    private readonly FrozenDictionary<string, AgentDefinition> _codeAgents;
    private readonly ToolCatalog _tools;
    private readonly AgentRuntimeServices _services;
    private readonly IAgentDefinitionStore? _store;

    internal AgentCatalog(
        IEnumerable<AgentDefinition> codeAgents,
        ToolCatalog tools,
        AgentRuntimeServices services,
        IAgentDefinitionStore? store)
    {
        CodeAgents = [.. codeAgents];

        var duplicates = CodeAgents
            .GroupBy(agent => agent.UniqueName)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException($"Several agents are named {string.Join(", ", duplicates)}.");

        _codeAgents = CodeAgents.ToFrozenDictionary(agent => agent.UniqueName);
        _tools = tools;
        _services = services;
        _store = store;
    }

    /// <summary>
    /// The agents defined in code, in registration order.
    /// </summary>
    public IReadOnlyList<AgentDefinition> CodeAgents { get; }

    /// <summary>
    /// The code agent with the given unique name.
    /// </summary>
    public AgentDefinition GetCodeAgent(string uniqueName) =>
        _codeAgents.GetValueOrDefault(uniqueName)
        ?? throw new ArgumentException($"No agent is named '{uniqueName}'.", nameof(uniqueName));

    /// <summary>
    /// The code agents, then the stored ones when an <see cref="IAgentDefinitionStore"/> is registered.
    /// </summary>
    public async Task<IReadOnlyList<AgentListing>> ListAsync(CancellationToken ct = default)
    {
        var stored = _store is null ? [] : await _store.ListAsync(ct);

        // A code agent has no display name of its own, its unique name stands in
        return
        [
            .. CodeAgents.Select(agent => new AgentListing(
                UniqueName: agent.UniqueName,
                DisplayName: agent.UniqueName,
                Description: agent.Description,
                IsStored: false)),
            .. stored.Select(agent => new AgentListing(
                UniqueName: agent.UniqueName,
                DisplayName: agent.DisplayName,
                Description: agent.Description,
                IsStored: true))
        ];
    }

    /// <summary>
    /// The runtime of the agent with the given unique name, defined in code or stored.
    /// </summary>
    public async Task<AgentRuntime> GetRuntimeAsync(string uniqueName, CancellationToken ct = default)
    {
        if (_codeAgents.TryGetValue(uniqueName, out var codeAgent)) return new AgentRuntime(codeAgent, _services);

        var stored = _store is null ? null : await _store.LoadAsync(uniqueName, ct);

        return stored is null
            ? throw new ArgumentException($"No agent is named '{uniqueName}'.", nameof(uniqueName))
            : CreateRuntime(stored);
    }

    /// <summary>
    /// The runtime of a stored agent; it fails when the agent names a tool this host does not offer.
    /// </summary>
    public AgentRuntime CreateRuntime(StoredAgent agent) =>
        new(new StoredAgentDefinition(agent, _tools, _services.Json), _services);

    /// <summary>
    /// A code agent as a stored agent: the same name, options, tools and tool settings, as data.
    /// </summary>
    public StoredAgent Export(AgentDefinition agent) => agent.ToStoredAgent(_services.Json);

    /// <summary>
    /// Stores the agent after checking that its name is free of code agents and that its tools exist.
    /// </summary>
    public async Task SaveAsync(StoredAgent agent, CancellationToken ct = default)
    {
        if (_store is null)
            throw new InvalidOperationException(
                "No IAgentDefinitionStore is registered: register one to store agents.");

        if (_codeAgents.ContainsKey(agent.UniqueName))
            throw new InvalidOperationException($"A code agent is named {agent.UniqueName} already.");

        CreateRuntime(agent);
        await _store.SaveAsync(agent, ct);
    }
}
