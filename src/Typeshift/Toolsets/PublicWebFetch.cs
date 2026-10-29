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

    internal static readonly TimeSpan TimeLimit = TimeSpan.FromSeconds(30);

    // shortcut: a NAT64 prefix an operator picks looks like any public network, so only the well-known one is
    // unwrapped; check the network's own prefix once a host runs behind one
    private static readonly IPNetwork Nat64 = IPNetwork.Parse("64:ff9b::/96");

    // Loopback, private, shared, link-local (the cloud's metadata service), benchmarking, documentation, multicast,
    // reserved, and the IPv6 forms that carry an IPv4 address nothing here can check
    private static readonly IPNetwork[] Refused =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/3"),
        IPNetwork.Parse("::/96"),
        IPNetwork.Parse("64:ff9b:1::/48"),
        IPNetwork.Parse("100::/64"),
        IPNetwork.Parse("2001::/32"),
        IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fec0::/10"),
        IPNetwork.Parse("ff00::/8")
    ];

    // The time limit is the TimeProvider's, so the client's own is off
    private readonly HttpClient _http = new(handler)
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public PublicWebFetch(TimeProvider time)
        : this(time, CreateHandler())
    {
    }

    internal static SocketsHttpHandler CreateHandler() =>
        new()
        {
            // Through a proxy, the callback would check the proxy's address and never the page's
            UseProxy = false,
            // One handler serves every run of every customer, so no cookie may carry over
            UseCookies = false,
            MaxAutomaticRedirections = 5,
            ConnectCallback = ConnectToPublicAsync
        };

    private static async ValueTask<Stream> ConnectToPublicAsync(
        SocketsHttpConnectionContext context,
        CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var resolved = await Dns.GetHostAddressesAsync(host, ct);
        var addresses = resolved.Where(IsPublic).ToArray();

        if (addresses.Length == 0)
            throw new HttpRequestException($"'{host}' has no public address; web_fetch reads public pages only.");

        // Dual mode, so one socket tries the IPv6 and the IPv4 addresses in turn
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
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
        var target = address.IsIPv4MappedToIPv6 || Nat64.Contains(address)
            ? new IPAddress(address.GetAddressBytes()[12..])
            : address;

        return !Refused.Any(network => network.Contains(target));
    }

    public async Task<WebPage> FetchAsync(Uri url, CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(TimeLimit, time);
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
            throw new TimeoutException($"'{url}' did not answer within {TimeLimit.TotalSeconds} seconds.");
        }
    }

    private static async Task<string> ReadAsync(
        Uri url,
        HttpContent content,
        MediaTypeHeaderValue? type,
        CancellationToken ct)
    {
        if (!IsText(type))
            throw new InvalidOperationException(
                $"'{url}' is {type?.MediaType ?? "of no named type"}; web_fetch reads text only.");

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var read = new MemoryStream();
        var buffer = new byte[81_920];

        while (read.Length < MaxBytes)
        {
            var room = (int)Math.Min(buffer.Length, MaxBytes - read.Length);
            var count = await stream.ReadAsync(buffer.AsMemory(0, room), ct);
            if (count == 0) break;

            read.Write(buffer, 0, count);
        }

        read.Position = 0;

        // A byte order mark names the encoding over the header, and stays out of the text
        using var reader = new StreamReader(read, GetEncoding(type), detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct);
    }

    private static bool IsText(MediaTypeHeaderValue? type) =>
        type?.MediaType?.ToLowerInvariant() is { } media
        && (media.StartsWith("text/", StringComparison.Ordinal)
            || media.EndsWith("/xml", StringComparison.Ordinal)
            || media.EndsWith("+xml", StringComparison.Ordinal)
            || media.EndsWith("/json", StringComparison.Ordinal)
            || media.EndsWith("+json", StringComparison.Ordinal));

    private static bool IsHtml(MediaTypeHeaderValue? type) =>
        type?.MediaType?.ToLowerInvariant() is "text/html" or "application/xhtml+xml";

    private static Encoding GetEncoding(MediaTypeHeaderValue? type)
    {
        if (type?.CharSet is not { } charset) return Encoding.UTF8;

        var name = charset.Trim('"');

        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(name) ?? Encoding.GetEncoding(name);
        }
        // Best effort, a charset no encoding knows reads as UTF-8
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static string? GetTitle(string html)
    {
        var match = Title().Match(html);
        if (!match.Success) return null;

        var title = Collapse(WebUtility.HtmlDecode(match.Groups[1].Value));

        return title.Length > 0
            ? title
            : null;
    }

    [GeneratedRegex(
        @"<title\b[^>]*>(.*?)</title\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Title();

    // Split() puts the text between <pre> blocks at even places and the blocks, which keep their lines, at odd ones
    private static string GetText(string html)
    {
        var visible = Hidden().Replace(html, "");
        var parts = Preformatted().Split(visible);

        var texts = parts
            .Select((part, index) => index % 2 == 0 ? GetFlowingText(part) : GetPreformattedText(part))
            .Where(text => text.Length > 0);

        return string.Join('\n', texts);
    }

    // Linear time, since the page is the model's choice and anyone's to write
    [GeneratedRegex(
        @"<!--.*?-->|<head\b.*?</head\s*>|<script\b.*?</script\s*>|<style\b.*?</style\s*>"
        + @"|<noscript\b.*?</noscript\s*>|<template\b.*?</template\s*>|<svg\b.*?</svg\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Hidden();

    [GeneratedRegex(
        @"(<pre\b.*?</pre\s*>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.NonBacktracking)]
    private static partial Regex Preformatted();

    private static string GetFlowingText(string html)
    {
        var collapsed = Spaces().Replace(html, " ");
        var blocks = Block().Replace(collapsed, "\n");
        var cells = Cell().Replace(blocks, " ");
        var text = WebUtility.HtmlDecode(Tag().Replace(cells, ""));

        var lines = text
            .Split('\n')
            .Select(Collapse)
            .Where(line => line.Length > 0);

        return string.Join('\n', lines);
    }

    [GeneratedRegex(
        @"</?(p|div|br|li|ul|ol|h[1-6]|tr|table|section|article|header|footer|nav|main|aside|blockquote|hr|dt|dd)"
        + @"\b[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Block();

    [GeneratedRegex(@"</?(td|th)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)]
    private static partial Regex Cell();

    private static string GetPreformattedText(string html)
    {
        var text = WebUtility.HtmlDecode(Tag().Replace(html, ""));
        return text.Trim('\r', '\n');
    }

    // A tag the size limit cut off ends the text; a '<' that opens no tag stays text
    [GeneratedRegex(@"<[a-zA-Z/!?][^>]*(>|\z)", RegexOptions.NonBacktracking)]
    private static partial Regex Tag();

    private static string Collapse(string text) => Spaces().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+", RegexOptions.NonBacktracking)]
    private static partial Regex Spaces();

    private static string Cut(string text) =>
        text.Length <= MaxLength
            ? text
            : text[..MaxLength] + "\n[The page goes on past this point.]";

    public void Dispose() => _http.Dispose();
}
