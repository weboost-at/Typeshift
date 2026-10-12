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
            .WithSettings<SearchArticlesSettings>()
            .Configurable(s => s.Language);

    /// <summary>
    /// Searches the help center's articles.
    /// </summary>
    /// <param name="question">The customer's question.</param>
    /// <param name="settings">The settings the search runs with.</param>
    public static IReadOnlyList<Article> SearchArticles(string question, SearchArticlesSettings settings) =>
        [.. Articles.Where(article => article.Language == settings.Language).Take(settings.Limit)];
}
