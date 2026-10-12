using Agentic.Json;
using Agentic.Settings;
using Agentic.StoredAgents;
using Agentic.Tools;
using Agentic.Toolsets;
using Microsoft.Extensions.AI;

namespace Agentic.Agents;

/// <summary>
/// The base of a partial class that declares an agent in its constructor.
/// </summary>
public abstract class AgentDefinition
{
    private readonly List<ToolDefinition> _tools = [];
    private readonly Dictionary<ToolName, object> _toolSettings = [];
    private ChatOptions _options = new();
    private string? _uniqueName;

    /// <summary>
    /// The lower_snake_case name the agent is stored and found under.
    /// </summary>
    public string UniqueName =>
        _uniqueName ?? throw new InvalidOperationException($"{GetType().Name} has no WithUniqueName(...).");

    /// <summary>
    /// What the agent does.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The tools this agent offers, in declaration order.
    /// </summary>
    public IReadOnlyList<ToolDefinition> Tools => _tools;

    /// <summary>
    /// The chat options that may change at runtime, as the generator lists them from the picks.
    /// </summary>
    protected virtual IReadOnlyList<ChatOptionProperty> ConfigurableChatOptions => [];

    /// <summary>
    /// Names the agent with a lower_snake_case constant, unique across everything the host can see.
    /// </summary>
    protected void WithUniqueName(string uniqueName)
    {
        if (!LowerSnakeCase.Matches(uniqueName))
            throw new ArgumentException($"'{uniqueName}' is not a lower_snake_case name.", nameof(uniqueName));

        _uniqueName = uniqueName;
    }

    /// <summary>
    /// Describes what the agent does.
    /// </summary>
    protected void WithDescription(string description) => Description = description;

    /// <summary>
    /// Sets the chat options the agent runs with; <paramref name="configure"/> picks the options that may change at
    /// runtime, for the generator to read.
    /// </summary>
    protected void WithChatOptions(
        ChatOptions options,
        Action<ConfigurableSettingsPicker<ChatOptions>>? configure = null) =>
        _options = options;

    /// <summary>
    /// Offers every tool of the toolset.
    /// </summary>
    protected void UseToolset<TToolset>() where TToolset : Toolset, new() => AddTools(new TToolset().Definitions);

    /// <summary>
    /// Offers the selected tools of the toolset, given as its static methods.
    /// </summary>
    protected void UseToolset<TToolset>(params Delegate[] tools) where TToolset : Toolset, new()
    {
        var available = new TToolset().Definitions;

        AddTools(tools.Select(tool => GetDefinition<TToolset>(available, tool)));
    }

    /// <summary>
    /// Sets the settings of every tool added so far that reads their type; they apply as a whole object, including
    /// members that are not allowlisted.
    /// </summary>
    protected void WithToolSettings(object settings)
    {
        var readers = _tools.Where(tool => tool.SettingsType == settings.GetType()).ToList();

        if (readers.Count == 0)
            throw new ArgumentException(
                $"No tool of {GetType().Name} reads {settings.GetType().Name}.",
                nameof(settings));

        foreach (var reader in readers)
        {
            _toolSettings[reader.Name] = settings;
        }
    }

    internal StoredAgent ToStoredAgent(AgentJson json) =>
        new()
        {
            UniqueName = UniqueName,
            DisplayName = UniqueName,
            Description = Description,
            Options = StoredAgentOptions.From(_options),
            Tools = [.. _tools.Select(tool => tool.Name)],
            ToolSettings = _toolSettings.ToDictionary(
                settings => settings.Key,
                settings => ExportSettings(settings.Key, settings.Value, json))
        };

    // A stored agent's name comes from storage, generated names are not lower_snake_case by design
    internal void SetUniqueName(string uniqueName) => _uniqueName = uniqueName;

    internal void Offer(IEnumerable<ToolDefinition> tools) => AddTools(tools);

    internal void SetToolSettings(ToolDefinition tool, object settings) => _toolSettings[tool.Name] = settings;

    // The one resolve path of code and stored agents: definition, then stored overrides, then the run
    internal ChatOptions CreateChatOptions(
        AgentConfiguration configuration,
        IReadOnlyDictionary<ToolName, ToolDefaults> toolDefaults,
        RunOverrides run,
        AgentJson json)
    {
        var options = _options.Clone();

        foreach (var property in ConfigurableChatOptions)
        {
            if (configuration.ChatOptions.TryGetValue(property.Name, out var stored))
                property.Apply(options, stored, json.SerializerOptions);

            if (run.ChatOptions is { } overrides) property.Override(options, overrides);
        }

        options.ConversationId = run.ChatOptions?.ConversationId ?? options.ConversationId;
        options.AdditionalProperties = (options.AdditionalProperties ?? []).WithExecutionContext(new(UniqueName));
        options.Tools =
        [
            .. options.Tools ?? [],
            .. _tools.Select(tool => CreateTool(tool, configuration, toolDefaults, run, json))
        ];

        return options;
    }

    private AITool CreateTool(
        ToolDefinition tool,
        AgentConfiguration configuration,
        IReadOnlyDictionary<ToolName, ToolDefaults> toolDefaults,
        RunOverrides run,
        AgentJson json)
    {
        var layers = new ToolSettingsLayers
        {
            Defaults = toolDefaults.GetValueOrDefault(tool.Name)?.Settings,
            Agent = _toolSettings.GetValueOrDefault(tool.Name),
            AgentOverrides = configuration.ToolSettings.GetValueOrDefault(tool.Name),
            Run = run.ToolSettings.FirstOrDefault(settings => settings.GetType() == tool.SettingsType)
        };

        return tool.Toolset.CreateTool(tool, layers, json);
    }

    private StoredSettings ExportSettings(ToolName name, object settings, AgentJson json)
    {
        var tool = _tools.First(offered => offered.Name == name);
        return tool.Toolset.ExportSettings(tool, settings, json);
    }

    // A method with optional parameters gets a delegate type per assembly, so the methods are compared, not delegates
    private static ToolDefinition GetDefinition<TToolset>(IReadOnlyList<ToolDefinition> available, Delegate tool) =>
        available.FirstOrDefault(definition => definition.Method.Method == tool.Method)
        ?? throw new ArgumentException($"A selected method is no tool of {typeof(TToolset).Name}.", nameof(tool));

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
