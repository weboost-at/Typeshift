using Agentic.Json;
using Agentic.Settings;
using Agentic.Tools;
using Microsoft.Extensions.AI;

namespace Agentic.Toolsets;

/// <summary>
/// The base of a partial class that declares its tools in its constructor.
/// </summary>
public abstract class Toolset
{
    private readonly List<ToolDefinition> _definitions = [];

    /// <summary>
    /// A human-readable name of the toolset.
    /// </summary>
    public string? DisplayName { get; protected set; }

    /// <summary>
    /// The tools of this toolset, in declaration order.
    /// </summary>
    public IReadOnlyList<ToolDefinition> Definitions => _definitions;

    /// <summary>
    /// Creates every tool of this toolset; passed settings apply to the tools reading their type, allowlisted
    /// members only.
    /// </summary>
    public IReadOnlyList<AIFunction> CreateTools(AgentJson json, params object[] settings) =>
        [.. _definitions.Select(tool => CreateTool(tool, RunningWith(tool, settings), json))];

    internal AIFunction CreateTool(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        GetBinding(tool).CreateFunction(tool, layers, json);

    internal object? ImportSettings(ToolDefinition tool, StoredSettings stored, AgentJson json) =>
        GetBinding(tool).ImportSettings(stored, json);

    internal StoredSettings ExportSettings(ToolDefinition tool, object settings, AgentJson json) =>
        GetBinding(tool).ExportSettings(settings, json);

    /// <summary>
    /// Declares a tool backed by a public static method; the generator reads the same chain at compile time.
    /// </summary>
    protected ToolBuilder AddTool(Delegate method)
    {
        var definition = new ToolDefinition(this, method);
        _definitions.Add(definition);
        return new ToolBuilder(definition);
    }

    private static ToolSettingsLayers RunningWith(ToolDefinition tool, object[] settings) =>
        new()
        {
            Run = settings.FirstOrDefault(candidate => candidate.GetType() == tool.SettingsType)
        };

    /// <summary>
    /// The generated binding of one of this toolset's tools.
    /// </summary>
    protected virtual ToolBinding GetBinding(ToolDefinition tool) =>
        throw new InvalidOperationException(
            $"{GetType().Name} has no generated tool '{tool.Name}'. "
            + "Declare it partial and reference the package in its project.");
}
