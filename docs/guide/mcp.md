# MCP servers

An agent can offer the tools of an MCP server next to its toolsets.

## Servers the host connects

`UseMcpServer` names a server:

<!-- snippet: use-mcp-server -->
<a id='snippet-use-mcp-server'></a>
```cs
UseMcpServer("status_page").ReadOnly();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L28-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-mcp-server' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The host connects it, with the MCP client of its choice, by implementing `IMcpToolProvider`. Typeshift takes no MCP
dependency; this provider uses the official `ModelContextProtocol.Core` client:

<!-- snippet: mcp-tool-provider -->
<a id='snippet-mcp-tool-provider'></a>
```cs
internal sealed class McpToolProvider : IMcpToolProvider, IAsyncDisposable
{
    private readonly Dictionary<string, McpClient> _clients = [];

    public async ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct)
    {
        if (!_clients.TryGetValue(server, out var client))
        {
            var transport = new HttpClientTransport(new() { Endpoint = new Uri($"https://mcp.example.com/{server}") });
            _clients[server] = client = await McpClient.CreateAsync(transport, cancellationToken: ct);
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
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/McpToolProvider.cs#L6-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-tool-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register it with `WithMcpToolProvider<T>()`. It is resolved from the run's services, scoped, so it may connect each
customer with their own credentials, and the scope disposes it with its connections:

<!-- snippet: mcp-registration -->
<a id='snippet-mcp-registration'></a>
```cs
services.AddTypeshift()
    .WithMcpToolProvider<McpToolProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L78-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The servers' tools come after the agent's own. `UseMcpServer(...).RequiresApproval()` asks the host before every call
of the server's tools ([tool approval](./tool-approval.md)); `ReadOnly()` marks them as reading only, so
[plan mode](./modes.md#plan-mode) offers them. A server tool named like one of the agent's own tools fails the run,
naming both, and so does an agent with servers but no provider.

Treat a server's tool descriptions and results as untrusted input.

## Servers the provider connects

Providers that call MCP servers themselves take Microsoft.Extensions.AI's `HostedMcpServerTool` in the chat options:

<!-- snippet: hosted-mcp -->
<a id='snippet-hosted-mcp'></a>
```cs
WithChatOptions()
    .WithModel("reasoning-model")
    .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/StatusAgent.cs#L9-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-hosted-mcp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
