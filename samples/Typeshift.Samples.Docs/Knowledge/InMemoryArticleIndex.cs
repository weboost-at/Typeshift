namespace Typeshift.Samples.Knowledge;

// The help center of the guide's scenarios: an article in English and one in German. A search finds the articles in
// the settings' language whose title starts with a word of the query
internal sealed class InMemoryArticleIndex : IArticleIndex
{
    private readonly List<(string Language, Article Article)> _articles =
    [
        ("en", new Article("refunds", "Refunds", "A refund reaches your account within five days.")),
        ("de", new Article("erstattungen", "Erstattungen", "Eine Erstattung ist binnen fünf Tagen auf Ihrem Konto."))
    ];

    public Task<IReadOnlyList<Article>> SearchAsync(string query, SearchArticlesSettings settings, CancellationToken ct)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        IReadOnlyList<Article> found =
        [
            .. _articles
                .Where(entry => entry.Language == settings.Language)
                .Select(entry => entry.Article)
                .Where(article => words.Any(word => article.Title.StartsWith(word, StringComparison.OrdinalIgnoreCase)))
                .Take(settings.Limit)
        ];

        return Task.FromResult(found);
    }

    public Task DeleteAsync(string slug, CancellationToken ct)
    {
        _articles.RemoveAll(entry => entry.Article.Slug == slug);
        return Task.CompletedTask;
    }
}
