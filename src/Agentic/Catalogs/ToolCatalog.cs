using System.Collections.Frozen;
using Agentic.Json;
using Agentic.Persistence;
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

    internal ToolCatalog(IEnumerable<Toolset> toolsets, AgentJson json)
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
    /// Finds a tool by name.
    /// </summary>
    /// <param name="name">The tool's name.</param>
    /// <returns>The tool's definition.</returns>
    /// <exception cref="ArgumentException">No tool has that name.</exception>
    public ToolDefinition GetDefinition(ToolName name) =>
        _tools.GetValueOrDefault(name) ?? throw new ArgumentException($"No tool is named '{name}'.", nameof(name));

    /// <summary>
    /// Creates a tool, running with its code defaults.
    /// </summary>
    /// <param name="name">The tool's name.</param>
    /// <returns>The tool's function.</returns>
    /// <exception cref="ArgumentException">No tool has that name.</exception>
    public AIFunction CreateTool(ToolName name)
    {
        var definition = GetDefinition(name);
        return definition.Toolset.CreateTool(definition, new ToolSettingsLayers(), _json);
    }

    /// <summary>
    /// Creates a tool whose configurable settings are overridden.
    /// </summary>
    /// <typeparam name="TSettings">The settings record of the tool.</typeparam>
    /// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
    /// <param name="tool">The tool, as its toolset's <c>Tools</c> class names it.</param>
    /// <param name="overrides">The values to run with instead of the code defaults.</param>
    /// <returns>The tool's function.</returns>
    /// <exception cref="ArgumentException">The tool is not registered.</exception>
    public AIFunction CreateTool<TSettings, TOverrides>(ToolRef<TSettings, TOverrides> tool, TOverrides overrides)
        where TSettings : class, new()
        where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
    {
        var definition = GetDefinition(tool.Name);
        return definition.Toolset.CreateTool(definition, new ToolSettingsLayers { Defaults = overrides }, _json);
    }
}
