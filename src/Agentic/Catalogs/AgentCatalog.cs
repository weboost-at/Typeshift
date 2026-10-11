using System.Collections.Frozen;
using Agentic.Agents;

namespace Agentic.Catalogs;

/// <summary>
/// Every agent the host can see, by unique name.
/// </summary>
public sealed class AgentCatalog
{
    private readonly FrozenDictionary<string, AgentDefinition> _codeAgents;

    /// <summary>
    /// Indexes the registered code agents by unique name.
    /// </summary>
    public AgentCatalog(IEnumerable<AgentDefinition> codeAgents)
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
}
