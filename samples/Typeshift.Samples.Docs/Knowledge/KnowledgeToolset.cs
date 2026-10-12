using Typeshift.Toolsets;

namespace Typeshift.Samples.Docs.Knowledge;

internal partial class KnowledgeToolset : Toolset
{
    private static readonly Article[] Articles =
    [
        new("How refunds work", "en"),
        new("So funktionieren Rückerstattungen", "de")
    ];

    public KnowledgeToolset()
    {
        // begin-snippet: settings-all-configurable
        AddTool(SearchArticles)
            .WithName("search_articles")
            .WithConfigurableSettings<SearchArticlesSettings>()
            .NotConfigurable(s => s.Limit);
        // end-snippet
    }

    /// <summary>
    /// Searches the help center's articles.
    /// </summary>
    /// <param name="question">The customer's question.</param>
    /// <param name="settings">The settings the search runs with.</param>
    public static IReadOnlyList<Article> SearchArticles(string question, SearchArticlesSettings settings) =>
        [.. Articles.Where(article => article.Language == settings.Language).Take(settings.Limit)];
}
