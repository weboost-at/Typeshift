using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Typeshift.Samples.Hosting;

// begin-snippet: mcp-tool-provider
internal sealed class McpToolProvider : IMcpToolProvider, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, McpClient> _clients = [];

    public async ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct)
    {
        if (!_clients.TryGetValue(server, out var client))
        {
            var transport = new HttpClientTransport(new() { Endpoint = new Uri($"https://mcp.example.com/{server}") });
            var connected = await McpClient.CreateAsync(transport, cancellationToken: ct);

            // Two sub-agents may connect a server at the same time: the first connection stays, the other closes
            client = _clients.GetOrAdd(server, connected);

            if (client != connected) await connected.DisposeAsync();
        }

        return [.. await client.ListToolsAsync(cancellationToken: ct)];
    }

    // The scope that resolved the provider disposes it, and its connections with it
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }
    }
}
// end-snippet
