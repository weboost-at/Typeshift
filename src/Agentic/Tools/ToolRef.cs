using Agentic.Settings;

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
/// A handle on a tool with settings: it carries the settings record and the view over its stored settings.
/// </summary>
public sealed record ToolRef<TSettings, TConfiguration>(ToolName Name) : ToolRef(Name)
    where TSettings : class, new()
    where TConfiguration : StoredSettingsView, IStoredSettingsView<TConfiguration>
{
    /// <summary>
    /// A handle on the tool with the given name.
    /// </summary>
    public ToolRef(string name) : this(new ToolName(name))
    {
    }
}
