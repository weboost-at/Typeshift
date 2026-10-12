using System.ComponentModel;
using Agentic.Json;
using Agentic.Settings;
using Agentic.Tools;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// The base of a partial class that declares an agent in its constructor: its chat options, the tools it offers
/// and the settings those tools run with. The generator adds the agent's typed <c>Settings</c> and a
/// <c>WithToolSettings</c> overload per settings type its tools read.
/// </summary>
public abstract class AgentDefinition
{
    private readonly List<ToolDefinition> _tools = [];
    private ChatOptions _options = new();
    private string? _name;

    /// <summary>
    /// The agent's name: the one given to <see cref="WithName"/>, or its class name. Conversations record it as the
    /// execution context they ran in.
    /// </summary>
    public string Name => _name ?? GetType().Name;

    /// <summary>
    /// What the agent does, if <see cref="WithDescription"/> said so.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The tools this agent offers, in the order its constructor adds them.
    /// </summary>
    public IReadOnlyList<ToolDefinition> Tools => _tools;

    /// <summary>
    /// Names the agent; without a name it goes by its class name.
    /// </summary>
    /// <param name="name">Any name, a compile-time constant.</param>
    protected void WithName(string name) => _name = name;

    /// <summary>
    /// Describes what the agent does.
    /// </summary>
    /// <param name="description">The description.</param>
    protected void WithDescription(string description) => Description = description;

    /// <summary>
    /// Sets the chat options the agent runs with.
    /// </summary>
    /// <param name="options">
    /// The options; set <see cref="ChatOptions.ModelId"/> for a model to run on by default.
    /// </param>
    /// <param name="configure">
    /// Picks the options that may change at runtime, <c>picker => picker.Configurable(o => o.Temperature)</c>. The
    /// generator reads the picks into the agent's <c>Settings</c>; at runtime the picker is never called.
    /// </param>
    protected void WithChatOptions(
        ChatOptions options,
        Action<ConfigurableSettingsPicker<ChatOptions>>? configure = null) =>
        _options = options;

    /// <summary>
    /// Offers every tool of a toolset.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <exception cref="InvalidOperationException">The agent already offers a tool of the same name.</exception>
    protected void UseToolset<TToolset>() where TToolset : Toolset, new() => AddTools(new TToolset().Definitions);

    /// <summary>
    /// Offers the selected tools of a toolset, <c>UseToolset&lt;CompanyToolset&gt;(tools => tools.FindCompanies())</c>.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <param name="select">Selects the tools through the methods the generator adds per tool.</param>
    /// <exception cref="ArgumentException">A selected tool is not a tool of the toolset.</exception>
    /// <exception cref="InvalidOperationException">The agent already offers a tool of the same name.</exception>
    protected void UseToolset<TToolset>(Action<ToolSelection<TToolset>> select) where TToolset : Toolset, new()
    {
        var available = new TToolset().Definitions;
        var selection = new ToolSelection<TToolset>();
        select(selection);

        AddTools(selection.Selected.Select(tool => available.FirstOrDefault(definition => definition.Name == tool)
            ?? throw new ArgumentException($"'{tool}' is no tool of {typeof(TToolset).Name}.", nameof(select))));
    }

    /// <summary>
    /// The settings the agent's code sets for a tool; the generator overrides it from the agent's
    /// <c>WithToolSettings</c> calls.
    /// </summary>
    /// <param name="tool">One of the agent's tools.</param>
    /// <returns>The tool's settings record, or null when the code sets none.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual object? GetToolSettings(ToolName tool) => null;

    // The one resolve path: the definition, then the stored settings, then the run's own
    internal ChatOptions CreateChatOptions(
        IAgentSettings? stored,
        IReadOnlyDictionary<ToolName, IToolOverrides> toolDefaults,
        IAgentSettings? run,
        string? conversationId,
        AgentJson json)
    {
        var options = _options.Clone();
        stored?.ApplyTo(options);
        run?.ApplyTo(options);

        options.ConversationId = conversationId ?? options.ConversationId;
        options.AdditionalProperties = (options.AdditionalProperties ?? []).WithExecutionContext(new(Name));

        options.Tools =
        [
            .. options.Tools ?? [],
            .. _tools.Select(tool => CreateTool(tool, stored, toolDefaults, run, json))
        ];

        return options;
    }

    private AITool CreateTool(
        ToolDefinition tool,
        IAgentSettings? stored,
        IReadOnlyDictionary<ToolName, IToolOverrides> toolDefaults,
        IAgentSettings? run,
        AgentJson json)
    {
        var layers = new ToolSettingsLayers
        {
            Defaults = toolDefaults.GetValueOrDefault(tool.Name),
            Agent = GetToolSettings(tool.Name),
            AgentOverrides = stored?.GetToolOverrides(tool.Name),
            Run = run?.GetToolOverrides(tool.Name)
        };

        return tool.Toolset.CreateTool(tool, layers, json);
    }

    private void AddTools(IEnumerable<ToolDefinition> tools)
    {
        foreach (var tool in tools)
        {
            if (_tools.Any(offered => offered.Name == tool.Name))
                throw new InvalidOperationException($"{GetType().Name} already offers a tool named '{tool.Name}'.");

            _tools.Add(tool);
        }
    }
}
