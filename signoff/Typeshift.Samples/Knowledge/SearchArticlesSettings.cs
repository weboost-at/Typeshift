namespace Typeshift.Samples.Knowledge;

// begin-snippet: settings-nested
internal record SearchArticlesSettings
{
    public string Language { get; init; } = "en";

    public int Limit { get; init; } = 5;

    public IReadOnlyList<string> Sources { get; init; } = ["help-center"];

    public ArticleRanking Ranking { get; init; } = new();
}

internal record ArticleRanking
{
    public double Recency { get; init; } = 0.3;

    public double Popularity { get; init; } = 0.2;
}
// end-snippet
