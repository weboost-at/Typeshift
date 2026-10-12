using Agentic.Persistence;
using Agentic.Settings;

namespace Agentic.Tools;

/// <summary>
/// A typed handle on a tool, generated per tool as <c>Toolset.Tools.Name</c>.
/// </summary>
/// <param name="Name">The tool's name.</param>
public record ToolRef(ToolName Name)
{
    /// <summary>
    /// A handle on the tool with the given name.
    /// </summary>
    /// <param name="name">The tool's lower_snake_case name.</param>
    public ToolRef(string name) : this(new ToolName(name))
    {
    }
}

/// <summary>
/// A handle on a tool with configurable settings: it carries the settings record and the generated overrides, so
/// APIs that take it infer both.
/// </summary>
/// <typeparam name="TSettings">The settings record of the tool.</typeparam>
/// <typeparam name="TOverrides">The generated overrides of the tool's configurable members.</typeparam>
/// <param name="Name">The tool's name.</param>
public sealed record ToolRef<TSettings, TOverrides>(ToolName Name) : ToolRef(Name)
    where TSettings : class, new()
    where TOverrides : class, IToolOverrides<TSettings>, IStorable<TOverrides>, new()
{
    /// <summary>
    /// A handle on the tool with the given name.
    /// </summary>
    /// <param name="name">The tool's lower_snake_case name.</param>
    public ToolRef(string name) : this(new ToolName(name))
    {
    }
}
