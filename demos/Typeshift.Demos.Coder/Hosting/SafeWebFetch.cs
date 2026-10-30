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
        var html = await _http.GetStringAsync(url, ct);
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

    private static bool IsPublic(IPAddress address) =>
        !IPAddress.IsLoopback(address)
        && !address.IsIPv6LinkLocal
        && !address.IsIPv6SiteLocal
        && !address.IsIPv6UniqueLocal
        && !IsPrivateV4(address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);

    private static bool IsPrivateV4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork) return false;

        var bytes = address.GetAddressBytes();

        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || (bytes[0] == 169 && bytes[1] == 254)
            || bytes[0] == 0;
    }

    [GeneratedRegex(@"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Title();

    [GeneratedRegex(@"<script[\s\S]*?</script>|<style[\s\S]*?</style>|<[^>]+>", RegexOptions.IgnoreCase)]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
