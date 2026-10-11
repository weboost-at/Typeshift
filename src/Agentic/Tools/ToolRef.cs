namespace Agentic.Tools;

/// <summary>
/// A typed handle on a tool, generated per tool as <c>Toolset.Tools.Name</c>.
/// </summary>
public record ToolRef(ToolName Name)
{
    /// <summary>
    /// A handle on the tool with the given name.
    /// </summary>
    public ToolRef(string name) : this(new ToolName(name))
    {
    }
}

/// <summary>
/// A handle on a tool that carries the type of the settings it reads.
/// </summary>
public sealed record ToolRef<TSettings>(ToolName Name) : ToolRef(Name) where TSettings : class, new()
{
    /// <summary>
    /// A handle on the tool with the given name.
    /// </summary>
    public ToolRef(string name) : this(new ToolName(name))
    {
    }
}
