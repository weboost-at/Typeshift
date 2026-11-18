using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Typeshift.Demos.Coder.Toolsets;

namespace Typeshift.Demos.Coder.Hosting;

// A fetcher that refuses loopback, private and link-local addresses where it connects, since the model picks the
// URLs; the page comes back as text with its tags stripped, capped
public sealed partial class SafeWebFetch : IDisposable
{
    private const int MaxLength = 20_000;

    private readonly HttpClient _http = new(new SocketsHttpHandler
    {
        ConnectCallback = ConnectPublicOnlyAsync,
        AllowAutoRedirect = false
    })
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    internal async Task<WebPage> FetchAsync(Uri url, CancellationToken ct)
    {
        string html;

        try
        {
            html = await _http.GetStringAsync(url, ct);
        }
        catch (HttpRequestException exception)
        {
            // The model picked the URL, so it reads why the fetch failed and can pick another
            throw new ToolException($"'{url}' could not be fetched: {exception.Message}", exception);
        }

        var title = Title().Match(html) is { Success: true } match ? match.Groups[1].Value.Trim() : null;
        var text = Whitespace().Replace(Tags().Replace(html, " "), " ").Trim();

        return new WebPage(url, title, text.Length <= MaxLength ? text : text[..MaxLength] + "…");
    }

    public void Dispose() => _http.Dispose();

    private static async ValueTask<Stream> ConnectPublicOnlyAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var address = addresses.FirstOrDefault(IsPublic)
            ?? throw new HttpRequestException($"'{context.DnsEndPoint.Host}' resolves to no public address.");

        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

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

    // An IPv4 address written as IPv6, such as ::ffff:127.0.0.2, is checked as the IPv4 address it reaches
    private static bool IsPublic(IPAddress address)
    {
        var reached = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        return !IPAddress.IsLoopback(reached)
            && !reached.Equals(IPAddress.IPv6Any)
            && !reached.IsIPv6LinkLocal
            && !reached.IsIPv6SiteLocal
            && !reached.IsIPv6UniqueLocal
            && !IsPrivateV4(reached);
    }

    // The private ranges, link-local, 0/8 and the carrier-grade NAT range 100.64/10; 127/8 is loopback
    private static bool IsPrivateV4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var bytes = address.GetAddressBytes();

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            || bytes[0] == 0;
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(@"<script[\s\S]*?</script>|<style[\s\S]*?</style>|<[^>]+>", RegexOptions.IgnoreCase)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
