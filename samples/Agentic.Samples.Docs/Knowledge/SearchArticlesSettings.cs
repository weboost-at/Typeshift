namespace Agentic.Samples.Docs.Knowledge;

internal record SearchArticlesSettings
{
    public string Language { get; init; } = "en";

    public int Limit { get; init; } = 3;
}
