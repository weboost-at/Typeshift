using Microsoft.Extensions.DependencyInjection;

namespace Typeshift.Samples.Knowledge;

internal partial class KnowledgeToolset : Toolset
{
    public KnowledgeToolset()
    {
        WithDisplayName("Help center");

        // begin-snippet: settings-all-configurable
        AddTool(SearchArticlesAsync)
            .WithConfigurableSettings<SearchArticlesSettings>()
            .NotConfigurable(s => s.Limit);
        // end-snippet

        AddTool(DeleteArticleAsync)
            .RequiresApproval();
    }

    /// <summary>
    /// Searches the help center's articles.
    /// </summary>
    /// <param name="query">What the customer asks about.</param>
    public static Task<IReadOnlyList<Article>> SearchArticlesAsync(
        string query,
        SearchArticlesSettings settings,
        IServiceProvider services,
        CancellationToken ct) =>
        services.GetRequiredService<IArticleIndex>().SearchAsync(query, settings, ct);

    /// <summary>
    /// Deletes an outdated article.
    /// </summary>
    /// <param name="slug">The article's slug.</param>
    public static Task DeleteArticleAsync(string slug, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<IArticleIndex>().DeleteAsync(slug, ct);
}
