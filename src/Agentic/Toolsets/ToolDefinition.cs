using Agentic.Tools;

namespace Agentic.Toolsets;

/// <summary>
/// A tool as its toolset's constructor declares it.
/// </summary>
public sealed class ToolDefinition
{
    private ToolName? _name;

    internal ToolDefinition(Toolset toolset, Delegate method)
    {
        Toolset = toolset;
        Method = method;
    }

    /// <summary>
    /// The toolset that declares this tool.
    /// </summary>
    public Toolset Toolset { get; }

    /// <summary>
    /// The static method the tool runs.
    /// </summary>
    public Delegate Method { get; }

    /// <summary>
    /// The name the tool is offered to the model under.
    /// </summary>
    public ToolName Name
    {
        get => _name ?? throw new InvalidOperationException(
            $"A tool of {Toolset.GetType().Name} has no WithName(...).");
        internal set => _name = value;
    }

    /// <summary>
    /// The description passed to <see cref="ToolBuilder.WithDescription"/>, if any.
    /// </summary>
    public string? Description { get; internal set; }

    /// <summary>
    /// The settings record the tool reads, if it has settings.
    /// </summary>
    public Type? SettingsType { get; internal set; }
}
