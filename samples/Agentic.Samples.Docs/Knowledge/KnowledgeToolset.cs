using Agentic.Tools;
using Agentic.Toolsets;

namespace Agentic.Samples.Docs.Knowledge;

internal partial class KnowledgeToolset : Toolset
{
    private static readonly Article[] Articles =
    [
        new("How refunds work", "en"),
        new("So funktionieren Rückerstattungen", "de")
    ];

    public KnowledgeToolset() =>
        AddTool(SearchArticles)
            .WithName("search_articles")
            .WithSettings<SearchArticlesSettings>(settings => settings
                .Configurable(s => s.Language));

    /// <summary>
    /// Searches the help center's articles.
    /// </summary>
    /// <param name="question">The customer's question.</param>
    /// <param name="services">The services of the call.</param>
    public static IReadOnlyList<Article> SearchArticles(string question, IServiceProvider services)
    {
        var settings = services.GetToolSettings<SearchArticlesSettings>();

        return [.. Articles.Where(article => article.Language == settings.Language).Take(settings.Limit)];
    }
}
