using Typeshift.Toolsets;

namespace Typeshift.Tools;

/// <summary>
/// The tools of <typeparamref name="TToolset"/>, to pick in <c>Include</c> and <c>Exclude</c>. The generator adds one
/// property per tool, named after it, so <c>UseToolset&lt;CompanyToolset&gt;().Include(t => t.FindCompanies)</c>
/// offers one tool.
/// </summary>
/// <typeparam name="TToolset">The toolset whose tools are listed.</typeparam>
public sealed class ToolList<TToolset> where TToolset : Toolset
{
    internal ToolList()
    {
    }
}
