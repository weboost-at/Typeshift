namespace Typeshift.Demos.Support.Toolsets;

internal sealed record Article(string Title, string Text);

internal partial class KnowledgeToolset : Toolset
{
    private static readonly Article[] Articles =
    [
        new("Logging in after an update", "Clear the browser's site data, then log in again; the session format changed."),
        new("Exporting to CSV", "Exports hold only the columns shown; add columns before you export."),
        new("Refunds", "Refunds go back to the original payment method within five business days.")
    ];

    public KnowledgeToolset()
    {
        WithDisplayName("Help center");

        AddTool(SearchArticles)
            .ReadOnly();
    }

    /// <summary>
    /// Finds help center articles by words of their title or text.
    /// </summary>
    /// <param name="query">Words to look for.</param>
    public static IReadOnlyList<Article> SearchArticles(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return
        [
            .. Articles.Where(article => words.Any(word =>
                article.Title.Contains(word, StringComparison.OrdinalIgnoreCase)
                || article.Text.Contains(word, StringComparison.OrdinalIgnoreCase)))
        ];
    }
}
