using Typeshift.Toolsets;

namespace Typeshift.Samples.Hosting;

internal sealed class HttpWebFetch(HttpClient http) : IWebFetch
{
    public async Task<WebPage> FetchAsync(Uri url, CancellationToken ct) =>
        new(url, Title: null, await http.GetStringAsync(url, ct));
}
