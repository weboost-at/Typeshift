using Typeshift.Tools;

namespace Typeshift.Toolsets;

/// <summary>
/// A tool as its toolset's constructor declares it.
/// </summary>
public sealed class ToolDefinition
{
    private ToolName? _name;

    internal ToolDefinition(Toolset toolset) => Toolset = toolset;

    /// <summary>
    /// The toolset that declares this tool.
    /// </summary>
    public Toolset Toolset { get; }

    /// <summary>
    /// The name the tool is offered to the model under.
    /// </summary>
    public ToolName Name
    {
        get => _name ?? throw new InvalidOperationException(
            $"A tool of {Toolset.GetType().Name} has no WithName(...).");
        internal set => _name = value;
    }
}
