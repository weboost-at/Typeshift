using System.Collections.Frozen;
using Agentic.Json;
using Agentic.Settings;
using Agentic.Tools;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Catalogs;

/// <summary>
/// Every tool the host can see, created by name without reflection.
/// </summary>
public sealed class ToolCatalog
{
    private readonly FrozenDictionary<ToolName, ToolDefinition> _tools;
    private readonly AgentJson _json;

    /// <summary>
    /// Indexes the tools of the registered toolsets by name.
    /// </summary>
    public ToolCatalog(IEnumerable<Toolset> toolsets, AgentJson json)
    {
        var definitions = toolsets.SelectMany(toolset => toolset.Definitions).ToList();

        var duplicates = definitions
            .GroupBy(definition => definition.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.Value)
            .ToList();

        if (duplicates.Count > 0)
            throw new InvalidOperationException($"Several tools are named {string.Join(", ", duplicates)}.");

        _tools = definitions.ToFrozenDictionary(definition => definition.Name);
        _json = json;
        Tools = definitions;
    }

    /// <summary>
    /// Every tool, in registration order.
    /// </summary>
    public IReadOnlyList<ToolDefinition> Tools { get; }

    /// <summary>
    /// The tool with the given name.
    /// </summary>
    public ToolDefinition GetDefinition(ToolName name) =>
        _tools.GetValueOrDefault(name) ?? throw new ArgumentException($"No tool is named '{name}'.", nameof(name));

    /// <summary>
    /// Creates the tool with the given name; stored settings change its allowlisted members.
    /// </summary>
    public AIFunction CreateTool(ToolName name, StoredSettings? storedSettings = null)
    {
        var definition = GetDefinition(name);

        var layers = new ToolSettingsLayers
        {
            Defaults = storedSettings
        };

        return definition.Toolset.CreateTool(definition, layers, _json);
    }
}
