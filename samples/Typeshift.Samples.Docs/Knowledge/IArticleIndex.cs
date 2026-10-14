namespace Typeshift.Samples.Knowledge;

internal interface IArticleIndex
{
    Task<IReadOnlyList<Article>> SearchAsync(string query, SearchArticlesSettings settings, CancellationToken ct);

    Task DeleteAsync(string slug, CancellationToken ct);
}
