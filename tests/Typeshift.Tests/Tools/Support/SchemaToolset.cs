using System.Text.Json;
using Typeshift.Toolsets;

namespace Typeshift.Tests.Tools.Support;

public enum SearchMode
{
    Fast,
    Thorough
}

public record Company(string Name, string? Country);

public readonly record struct GeoPoint(double Latitude, double Longitude);

public partial class SchemaToolset : Toolset
{
    public SchemaToolset()
    {
        AddTool(SearchCompanies)
            .WithName("search_companies")
            .WithDescription("Searches companies and returns the limit it searched with.");

        AddTool(Locate)
            .WithName("locate")
            .WithDescription("Returns the point it was given.");

        AddTool(CountWords)
            .WithName("count_words");

        AddTool(Echo)
            .WithName("echo")
            .WithDescription("Returns the JSON it was given.");
    }

    /// <param name="query">What to search for.</param>
    /// <param name="country">Two-letter ISO country code, or null for every country.</param>
    /// <param name="known">Companies the search already knows.</param>
    /// <param name="limit">The most results to return.</param>
    /// <param name="mode">How thoroughly to search.</param>
    public static int SearchCompanies(
        string query,
        string? country,
        Company[] known,
        int limit = 10,
        SearchMode mode = SearchMode.Fast) =>
        limit;

    public static GeoPoint Locate(GeoPoint point = default) => point;

    /// <summary>
    /// Counts the words of a text.
    /// </summary>
    /// <param name="text">The text whose words are counted.</param>
    public static int CountWords(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    public static JsonElement Echo(JsonElement value) => value;
}
