using Agentic.Toolsets;

namespace Agentic.Agents;

/// <summary>
/// The base of a partial class that declares an agent in its constructor.
/// </summary>
public abstract class AgentDefinition
{
    private readonly List<ToolDefinition> _tools = [];
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
    /// Names the agent with a lower_snake_case constant, unique across everything the host can see.
    /// </summary>
    protected AgentDefinition WithUniqueName(string uniqueName)
    {
        if (!LowerSnakeCase.Matches(uniqueName))
            throw new ArgumentException($"'{uniqueName}' is not a lower_snake_case name.", nameof(uniqueName));

        _uniqueName = uniqueName;
        return this;
    }

    /// <summary>
    /// Describes what the agent does.
    /// </summary>
    protected AgentDefinition WithDescription(string description)
    {
        Description = description;
        return this;
    }

    /// <summary>
    /// Offers every tool of the toolset.
    /// </summary>
    protected AgentDefinition UseToolset<TToolset>() where TToolset : Toolset, new()
    {
        AddTools(new TToolset().Definitions);
        return this;
    }

    /// <summary>
    /// Offers the selected tools of the toolset, given as its static methods.
    /// </summary>
    protected AgentDefinition UseToolset<TToolset>(params Delegate[] tools) where TToolset : Toolset, new()
    {
        var available = new TToolset().Definitions;

        AddTools(tools.Select(tool => GetDefinition<TToolset>(available, tool)));
        return this;
    }

    // Delegates are equal when they point at the same method, no MethodInfo needed
    private static ToolDefinition GetDefinition<TToolset>(IReadOnlyList<ToolDefinition> available, Delegate tool) =>
        available.FirstOrDefault(definition => definition.Method.Equals(tool))
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
