using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Typeshift.Toolsets;

namespace Typeshift.Samples.Hosting;

// begin-snippet: web-search
internal sealed class SearchApi(HttpClient http) : IWebSearch
{
    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int count, CancellationToken ct)
    {
        var url = $"https://search.example.com/?q={Uri.EscapeDataString(query)}&count={count}";
        var hits = await http.GetFromJsonAsync(url, SearchApiJson.Default.SearchHitArray, ct) ?? [];

        return [.. hits.Select(hit => new WebSearchResult(hit.Title, hit.Url, hit.Snippet))];
    }
}
// end-snippet

internal sealed record SearchHit(string Title, Uri Url, string Snippet);

[JsonSerializable(typeof(SearchHit[]))]
internal partial class SearchApiJson : JsonSerializerContext;
