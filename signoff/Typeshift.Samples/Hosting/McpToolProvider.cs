using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Typeshift.Samples.Hosting;

// begin-snippet: mcp-tool-provider
internal sealed class McpToolProvider : IMcpToolProvider
{
    public async ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct)
    {
        var transport = new HttpClientTransport(new() { Endpoint = new Uri($"https://mcp.example.com/{server}") });
        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        return [.. await client.ListToolsAsync(cancellationToken: ct)];
    }
}
// end-snippet
