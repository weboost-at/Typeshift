namespace Typeshift;

// D4: the generator reads the picks; at runtime the calls declare nothing
/// <summary>
/// An agent's use of a toolset. One use includes or excludes, never both, and may require approval for a tool or
/// let settings switch one off.
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

    /// <summary>
    /// Pauses a run that calls the tool picked until your app approves or rejects the call, as the toolset's own
    /// <c>RequiresApproval()</c> does, in this agent only.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/tool-approval#requiring-approval">Requiring
    /// approval</see>.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.CloseTicket</c>.</param>
    /// <returns>The same use.</returns>
    public ToolsetUse<TToolset> RequiresApproval(Func<ToolList<TToolset>, ToolName> tool) => this;

    /// <summary>
    /// Lets stored settings or a run switch the tool picked off: the agent's <c>Settings</c> gain a <c>bool?</c>
    /// member named after the tool with <c>Enabled</c>, and a layer that sets it to false leaves the tool out of the
    /// run. The tool is offered while no layer does.
    /// More in the guide:
    /// <see href="https://weboost-at.github.io/Typeshift/guide/defining-agents#switching-a-tool-off">Switching a
    /// tool off</see>.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.SearchWeb</c>.</param>
    /// <returns>The same use.</returns>
    public ToolsetUse<TToolset> Configurable(Func<ToolList<TToolset>, ToolName> tool) => this;
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

    /// <summary>
    /// Pauses a run that calls the tool picked, one the use includes, until your app approves or rejects the call.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.CloseTicket</c>.</param>
    /// <returns>The same use.</returns>
    public ToolInclusion<TToolset> RequiresApproval(Func<ToolList<TToolset>, ToolName> tool) => this;

    /// <summary>
    /// Lets stored settings or a run switch the tool picked, one the use includes, off.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.SearchWeb</c>.</param>
    /// <returns>The same use.</returns>
    public ToolInclusion<TToolset> Configurable(Func<ToolList<TToolset>, ToolName> tool) => this;
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

    /// <summary>
    /// Pauses a run that calls the tool picked, one the use does not exclude, until your app approves or rejects the
    /// call.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.CloseTicket</c>.</param>
    /// <returns>The same use.</returns>
    public ToolExclusion<TToolset> RequiresApproval(Func<ToolList<TToolset>, ToolName> tool) => this;

    /// <summary>
    /// Lets stored settings or a run switch the tool picked, one the use does not exclude, off.
    /// </summary>
    /// <param name="tool">The tool, <c>t => t.SearchWeb</c>.</param>
    /// <returns>The same use.</returns>
    public ToolExclusion<TToolset> Configurable(Func<ToolList<TToolset>, ToolName> tool) => this;
}
