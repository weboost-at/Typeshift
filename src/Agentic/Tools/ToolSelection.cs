using System.ComponentModel;
using Agentic.Toolsets;

namespace Agentic.Tools;

/// <summary>
/// The tools an agent offers from <typeparamref name="TToolset"/>: all of them, or only those chained after
/// <c>UseToolset</c>. The generator adds one method per tool, named after it, so
/// <c>UseToolset&lt;CompanyToolset&gt;().FindCompanies().CreateCompanyProfile()</c> offers two tools.
/// </summary>
/// <typeparam name="TToolset">The toolset to select from.</typeparam>
public sealed class ToolSelection<TToolset> where TToolset : Toolset
{
    private readonly TToolset _toolset;
    private readonly List<ToolDefinition> _selected = [];

    internal ToolSelection(TToolset toolset) => _toolset = toolset;

    // D41: nothing selected offers the whole toolset
    internal IReadOnlyList<ToolDefinition> Tools => _selected.Count == 0 ? _toolset.Definitions : _selected;

    // D35: public for the generated selection methods in other assemblies, hidden from editors
    /// <summary>
    /// Selects a tool by name; the generated methods call it.
    /// </summary>
    /// <param name="tool">The tool's name.</param>
    /// <returns>This selection, to select further tools.</returns>
    /// <exception cref="ArgumentException">The toolset has no tool of that name.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ToolSelection<TToolset> Select(string tool)
    {
        _selected.Add(_toolset.Definitions.FirstOrDefault(definition => definition.Name.Value == tool)
            ?? throw new ArgumentException($"'{tool}' is no tool of {typeof(TToolset).Name}.", nameof(tool)));

        return this;
    }
}
