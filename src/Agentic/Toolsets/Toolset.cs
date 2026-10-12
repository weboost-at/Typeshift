using System.ComponentModel;
using Agentic.Json;
using Agentic.Persistence;
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
    /// Creates every tool of this toolset, outside of any agent.
    /// </summary>
    /// <param name="json">The serializer and schema options the functions bind arguments and results with.</param>
    /// <param name="overrides">
    /// Overrides of the tools' configurable settings, one per tool at most; a tool without any runs with its code
    /// defaults.
    /// </param>
    /// <returns>One function per tool, in declaration order.</returns>
    public IReadOnlyList<AIFunction> CreateTools(AgentJson json, params IToolOverrides[] overrides) =>
        [.. _definitions.Select(tool => CreateTool(tool, RunningWith(tool, overrides), json))];

    internal AIFunction CreateTool(ToolDefinition tool, ToolSettingsLayers layers, AgentJson json) =>
        GetBinding(tool).CreateFunction(tool, layers, json);

    internal Task<IToolOverrides?> LoadDefaultsAsync<TId>(
        ToolDefinition tool,
        IAgenticStore<TId> store,
        TId id,
        CancellationToken ct)
        where TId : notnull =>
        GetBinding(tool).LoadDefaultsAsync(store, id, ct);

    /// <summary>
    /// Declares a tool backed by a public static method; the generator reads the same chain at compile time.
    /// </summary>
    /// <param name="method">The tool's method, as a method group: <c>AddTool(FindCompanies)</c>.</param>
    /// <returns>A builder that names, describes and configures the tool.</returns>
    protected ToolBuilder AddTool(Delegate method)
    {
        var definition = new ToolDefinition(this, method);
        _definitions.Add(definition);
        return new ToolBuilder(definition);
    }

    private static ToolSettingsLayers RunningWith(ToolDefinition tool, IToolOverrides[] overrides) =>
        new()
        {
            Run = overrides.FirstOrDefault(candidate => candidate.Tool == tool.Name)
        };

    /// <summary>
    /// The generated binding of one of this toolset's tools; the generator overrides it.
    /// </summary>
    /// <param name="tool">One of this toolset's tools.</param>
    /// <returns>The tool's binding.</returns>
    /// <exception cref="InvalidOperationException">The toolset was not compiled with the generator.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual ToolBinding GetBinding(ToolDefinition tool) =>
        throw new InvalidOperationException(
            $"{GetType().Name} has no generated tool '{tool.Name}'. "
            + "Declare it partial and reference the package in its project.");
}
