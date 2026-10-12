using System.ComponentModel;
using Agentic.Json;
using Agentic.Settings;
using Agentic.Tools;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// The base of a partial class that declares an agent in its constructor: its chat options, the tools it offers
/// and the settings those tools run with. The generator adds the agent's typed <c>Settings</c> and
/// <c>WithToolSettings()</c>, a builder over the settings of its tools.
/// </summary>
public abstract class AgentDefinition
{
    private readonly List<Func<IReadOnlyList<ToolDefinition>>> _uses = [];
    private readonly ChatOptions _options = new();
    private IReadOnlyList<ToolDefinition>? _tools;
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
    /// <exception cref="InvalidOperationException">The agent offers two tools of the same name.</exception>
    public IReadOnlyList<ToolDefinition> Tools => _tools ??= GetTools();

    /// <summary>
    /// The agent's instructions, its system prompt. Override it to define them, in a raw string literal say; the
    /// agent's settings may override them where <c>Configurable(o => o.Instructions)</c> allows it.
    /// </summary>
    /// <returns>The instructions, or null for none; called whenever a run's options are resolved.</returns>
    protected virtual string? GetInstructions() => null;

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
    /// Sets the chat options the agent runs with: <c>WithChatOptions().WithModel("fast-model").WithTemperature(0.3f)
    /// .Configurable(o => o.Temperature)</c>.
    /// </summary>
    /// <returns>A builder that sets the options and marks those that may change at runtime.</returns>
    protected ChatOptionsBuilder WithChatOptions() => new(_options);

    /// <summary>
    /// Offers the tools of a toolset: every tool, or only those chained after the call,
    /// <c>UseToolset&lt;CompanyToolset&gt;().FindCompanies()</c>.
    /// </summary>
    /// <typeparam name="TToolset">The toolset.</typeparam>
    /// <returns>A selection with one method per tool, generated.</returns>
    protected ToolSelection<TToolset> UseToolset<TToolset>() where TToolset : Toolset, new()
    {
        var selection = new ToolSelection<TToolset>(new TToolset());
        _uses.Add(() => selection.Tools);
        return selection;
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
        // D42: called per resolve, never from the constructor
        options.Instructions = GetInstructions();
        stored?.ApplyTo(options);
        run?.ApplyTo(options);

        options.ConversationId = conversationId ?? options.ConversationId;
        options.AdditionalProperties = (options.AdditionalProperties ?? []).WithExecutionContext(new(Name));

        options.Tools =
        [
            .. options.Tools ?? [],
            .. Tools.Select(tool => CreateTool(tool, stored, toolDefaults, run, json))
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

    private List<ToolDefinition> GetTools()
    {
        var tools = new List<ToolDefinition>();

        foreach (var tool in _uses.SelectMany(use => use()))
        {
            if (tools.Any(offered => offered.Name == tool.Name))
                throw new InvalidOperationException($"{GetType().Name} offers two tools named '{tool.Name}'.");

            tools.Add(tool);
        }

        return tools;
    }
}
