using Microsoft.Extensions.DependencyInjection;
using Typeshift.Demos.Coder.Hosting;

namespace Typeshift.Demos.Coder.Toolsets;

// Fetching only reads, so plan mode lets it through; SafeWebFetch keeps the model's URLs off the local network
internal partial class WebToolset : Toolset
{
    public WebToolset()
    {
        AddTool(WebFetchAsync)
            .ReadOnly();
    }

    /// <summary>
    /// Fetches a web page as text, such as a library's documentation.
    /// </summary>
    /// <param name="url">An absolute http or https URL.</param>
    /// <param name="services">The run's services.</param>
    /// <param name="ct">Cancels the fetch.</param>
    /// <returns>The page's title and text.</returns>
    public static Task<WebPage> WebFetchAsync(Uri url, IServiceProvider services, CancellationToken ct) =>
        url is { IsAbsoluteUri: true, Scheme: "http" or "https" }
            ? services.GetRequiredService<SafeWebFetch>().FetchAsync(url, ct)
            : throw new ArgumentException($"'{url}' is no absolute http or https URL.", nameof(url));
}

/// <summary>
/// A fetched page.
/// </summary>
/// <param name="Url">Where it was fetched from.</param>
/// <param name="Title">Its title, if it has one.</param>
/// <param name="Content">Its text, without tags, capped.</param>
internal sealed record WebPage(Uri Url, string? Title, string Content);
