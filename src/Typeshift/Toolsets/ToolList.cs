namespace Typeshift;

/// <summary>
/// The tools of <typeparamref name="TToolset"/>, to pick from in <c>Include</c> and <c>Exclude</c>. The generator adds
/// one property per tool, <c>t.SearchTickets</c>; a tool of another toolset has none, so picking it does not compile.
/// </summary>
/// <typeparam name="TToolset">The toolset.</typeparam>
public sealed class ToolList<TToolset> where TToolset : Toolset
{
    private ToolList()
    {
    }
}
