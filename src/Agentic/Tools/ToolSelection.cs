using System.ComponentModel;
using Agentic.Toolsets;

namespace Agentic.Tools;

/// <summary>
/// The tools an agent selects from <typeparamref name="TToolset"/>. The generator adds one method per tool, named
/// after it, so <c>tools => tools.FindCompanies().CreateCompanyProfile()</c> selects two tools.
/// </summary>
/// <typeparam name="TToolset">The toolset to select from.</typeparam>
public sealed class ToolSelection<TToolset> where TToolset : Toolset
{
    private readonly List<ToolName> _selected = [];

    internal ToolSelection()
    {
    }

    internal IReadOnlyList<ToolName> Selected => _selected;

    /// <summary>
    /// Selects a tool by name; the generated methods call it.
    /// </summary>
    /// <param name="tool">The tool's name.</param>
    /// <returns>This selection, to select further tools.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ToolSelection<TToolset> Select(string tool)
    {
        _selected.Add(new ToolName(tool));
        return this;
    }
}
