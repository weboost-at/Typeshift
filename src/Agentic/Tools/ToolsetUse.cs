using Agentic.Toolsets;

namespace Agentic.Tools;

/// <summary>
/// The tools an agent offers from <typeparamref name="TToolset"/>: all of them, or only those it includes, or all but
/// those it excludes. One <c>UseToolset</c> call either includes or excludes.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolsetUse<TToolset> where TToolset : Toolset
{
    private readonly TToolset _toolset;
    private readonly List<ToolDefinition> _picked = [];
    private bool _excludes;

    internal ToolsetUse(TToolset toolset) => _toolset = toolset;

    // D41: nothing picked offers the whole toolset
    internal IReadOnlyList<ToolDefinition> Tools =>
        _excludes || _picked.Count == 0 ? [.. _toolset.Definitions.Except(_picked)] : _picked;

    /// <summary>
    /// Offers only the tools included, in the order they are included.
    /// </summary>
    /// <param name="tool">Picks a tool from the generated list, <c>t => t.FindCompanies</c>.</param>
    /// <returns>A chain that includes further tools.</returns>
    /// <exception cref="ArgumentException">The tool is not a tool of the toolset.</exception>
    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolRef> tool) => new(Pick(tool));

    /// <summary>
    /// Offers every tool of the toolset but the ones excluded.
    /// </summary>
    /// <param name="tool">Picks a tool from the generated list, <c>t => t.FindCompanies</c>.</param>
    /// <returns>A chain that excludes further tools.</returns>
    /// <exception cref="ArgumentException">The tool is not a tool of the toolset.</exception>
    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolRef> tool)
    {
        _excludes = true;
        return new(Pick(tool));
    }

    internal ToolsetUse<TToolset> Pick(Func<ToolList<TToolset>, ToolRef> tool)
    {
        var name = tool(new ToolList<TToolset>()).Name;

        _picked.Add(_toolset.Definitions.FirstOrDefault(definition => definition.Name == name)
            ?? throw new ArgumentException($"'{name}' is no tool of {typeof(TToolset).Name}.", nameof(tool)));

        return this;
    }
}

/// <summary>
/// The tools an agent includes from <typeparamref name="TToolset"/>, in order.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolInclusion<TToolset> where TToolset : Toolset
{
    private readonly ToolsetUse<TToolset> _use;

    internal ToolInclusion(ToolsetUse<TToolset> use) => _use = use;

    /// <summary>
    /// Offers one more tool of the toolset.
    /// </summary>
    /// <param name="tool">Picks a tool from the generated list, <c>t => t.CreateCompanyProfile</c>.</param>
    /// <returns>The same chain, to include further tools.</returns>
    /// <exception cref="ArgumentException">The tool is not a tool of the toolset.</exception>
    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolRef> tool)
    {
        _use.Pick(tool);
        return this;
    }
}

/// <summary>
/// The tools an agent leaves out of <typeparamref name="TToolset"/>.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolExclusion<TToolset> where TToolset : Toolset
{
    private readonly ToolsetUse<TToolset> _use;

    internal ToolExclusion(ToolsetUse<TToolset> use) => _use = use;

    /// <summary>
    /// Leaves one more tool of the toolset out.
    /// </summary>
    /// <param name="tool">Picks a tool from the generated list, <c>t => t.CreateCompanyProfile</c>.</param>
    /// <returns>The same chain, to exclude further tools.</returns>
    /// <exception cref="ArgumentException">The tool is not a tool of the toolset.</exception>
    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolRef> tool)
    {
        _use.Pick(tool);
        return this;
    }
}
