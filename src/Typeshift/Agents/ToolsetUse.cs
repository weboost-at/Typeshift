namespace Typeshift;

// D4: the generator reads the picks; at runtime the calls declare nothing
/// <summary>
/// An agent's use of a toolset. One use includes or excludes, never both.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolsetUse<TToolset> where TToolset : Toolset
{
    internal static readonly ToolsetUse<TToolset> Instance = new();

    private ToolsetUse()
    {
    }

    /// <summary>
    /// Offers only the tools picked, in the order picked.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.SearchTickets</c>.</param>
    /// <returns>The use, to include more.</returns>
    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolName> tool) => ToolInclusion<TToolset>.Instance;

    /// <summary>
    /// Offers every tool but those picked.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.DeleteArticle</c>.</param>
    /// <returns>The use, to exclude more.</returns>
    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolName> tool) => ToolExclusion<TToolset>.Instance;
}

/// <summary>
/// A use that offers only the tools it includes.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolInclusion<TToolset> where TToolset : Toolset
{
    internal static readonly ToolInclusion<TToolset> Instance = new();

    private ToolInclusion()
    {
    }

    /// <summary>
    /// Offers one more tool.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.CloseTicket</c>.</param>
    /// <returns>The same use.</returns>
    public ToolInclusion<TToolset> Include(Func<ToolList<TToolset>, ToolName> tool) => this;
}

/// <summary>
/// A use that offers every tool but those it excludes.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolExclusion<TToolset> where TToolset : Toolset
{
    internal static readonly ToolExclusion<TToolset> Instance = new();

    private ToolExclusion()
    {
    }

    /// <summary>
    /// Leaves out one more tool.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The same use.</returns>
    public ToolExclusion<TToolset> Exclude(Func<ToolList<TToolset>, ToolName> tool) => this;
}
