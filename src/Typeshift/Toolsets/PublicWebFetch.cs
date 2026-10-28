using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Typeshift.Toolsets;

// D37: the fetcher WithWebFetch() registers. The model picks the URL, so it connects only to public addresses,
// checked where it connects and so for each redirect too, and reads a page's text within a time and a size limit
internal sealed partial class PublicWebFetch(TimeProvider time, HttpMessageHandler handler) : IWebFetch, IDisposable
{
    // As much as the file tools return in one call
    internal const int MaxLength = 65_536;

    // A page is mostly markup, so its HTML may be many times its text
    internal const int MaxBytes = 4 * 1024 * 1024;

    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Loopback, private, shared (carrier-grade NAT), link-local (the cloud's metadata service), multicast, reserved
    private static readonly IPNetwork[] Refused =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("224.0.0.0/3"),
        IPNetwork.Parse("::/127"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fec0::/10"),
        IPNetwork.Parse("ff00::/8")
    ];

    // The time limit is the TimeProvider's, so the client's own is off
    private readonly HttpClient _http = new(handler)
    {
        Timeout = System.Threading.Timeout.InfiniteTimeSpan
    };

    public PublicWebFetch(TimeProvider time)
        : this(time, CreateHandler())
    {
    }

    public async Task<WebPage> FetchAsync(Uri url, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(Timeout, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            response.EnsureSuccessStatusCode();

            var type = response.Content.Headers.ContentType;
            var body = await ReadAsync(url, response.Content, type, linked.Token);
            var page = response.RequestMessage?.RequestUri ?? url;

            return IsHtml(type)
                ? new WebPage(page, GetTitle(body), Cut(GetText(body)))
                : new WebPage(page, Title: null, Cut(body));
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException($"'{url}' did not answer within {Timeout.TotalSeconds} seconds.");
        }
    }

    // The first MaxBytes of the body, in the charset the server names, UTF-8 when it names none it knows
    private static async Task<string> ReadAsync(
        Uri url,
        HttpContent content,
        MediaTypeHeaderValue? type,
        CancellationToken ct)
    {
        if (!IsText(type))
            throw new InvalidOperationException($"'{url}' is {type?.MediaType}, and web_fetch reads text only.");

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var read = new MemoryStream();
        var buffer = new byte[81_920];

        while (read.Length < MaxBytes && await stream.ReadAsync(Rest(buffer, read), ct) is var count and > 0)
        {
            read.Write(buffer, 0, count);
        }

        return GetEncoding(type).GetString(read.GetBuffer(), 0, (int)read.Length);
    }

    private static Memory<byte> Rest(byte[] buffer, MemoryStream read) =>
        buffer.AsMemory(0, (int)Math.Min(buffer.Length, MaxBytes - read.Length));

    private static bool IsText(MediaTypeHeaderValue? type) =>
        type?.MediaType is not { } media
        || media.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
        || media.Contains("html", StringComparison.OrdinalIgnoreCase)
        || media.Contains("xml", StringComparison.OrdinalIgnoreCase)
        || media.Contains("json", StringComparison.OrdinalIgnoreCase);

    private static bool IsHtml(MediaTypeHeaderValue? type) =>
        type?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false;

    private static Encoding GetEncoding(MediaTypeHeaderValue? type)
    {
        try
        {
            return type?.CharSet is { } charset ? Encoding.GetEncoding(charset.Trim('"')) : Encoding.UTF8;
        }
        // Best effort, a charset the runtime does not know reads as UTF-8
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static string? GetTitle(string html) =>
        Title().Match(html) is { Success: true } match
        && Collapse(WebUtility.HtmlDecode(match.Groups[1].Value)) is { Length: > 0 } title
            ? title
            : null;

    // Whitespace collapses as a browser collapses it, each block becomes a line, and entities become characters
    private static string GetText(string html)
    {
        var visible = Spaces().Replace(Hidden().Replace(html, ""), " ");
        var text = WebUtility.HtmlDecode(Tag().Replace(Block().Replace(visible, "\n"), ""));

        var lines = text
            .Split('\n')
            .Select(Collapse)
            .Where(line => line.Length > 0);

        return string.Join('\n', lines);
    }

    private static string Collapse(string text) => Spaces().Replace(text, " ").Trim();

    private static string Cut(string text) =>
        text.Length <= MaxLength
            ? text
            : text[..MaxLength] + "\n[The page goes on past this point.]";

    private static SocketsHttpHandler CreateHandler() =>
        new()
        {
            ConnectCallback = ConnectToPublicAsync,
            // Through a proxy, the callback would check the proxy's address and never the page's
            UseProxy = false,
            MaxAutomaticRedirections = 5
        };

    private static async ValueTask<Stream> ConnectToPublicAsync(
        SocketsHttpConnectionContext context,
        CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = await Dns.GetHostAddressesAsync(host, ct);

        var address = addresses.FirstOrDefault(IsPublic)
            ?? throw new HttpRequestException($"'{host}' has no public address; web_fetch reads public pages only.");

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(address, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static bool IsPublic(IPAddress address)
    {
        var target = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return !Refused.Any(network => network.Contains(target));
    }

    // Linear time, since the page is the model's choice and anyone's to write
    [GeneratedRegex(
        @"<!--.*?-->|<head\b.*?</head\s*>|<script\b.*?</script\s*>|<style\b.*?</style\s*>"
        + @"|<noscript\b.*?</noscript\s*>|<template\b.*?</template\s*>|<svg\b.*?</svg\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Hidden();

    [GeneratedRegex(
        @"</?(p|div|br|li|ul|ol|h[1-6]|tr|table|section|article|header|footer|nav|main|aside|blockquote|pre|hr|dt|dd)"
        + @"\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Block();

    // A tag the size limit cut off ends the text
    [GeneratedRegex(@"<[^>]*(>|\z)", RegexOptions.NonBacktracking)]
    private static partial Regex Tag();

    [GeneratedRegex(
        @"<title\b[^>]*>(.*?)</title\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Title();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Spaces();

    public void Dispose() => _http.Dispose();
}
