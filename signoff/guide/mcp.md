# MCP servers

An agent can offer the tools of an MCP server next to its toolsets.

## Servers the host connects

`UseMcpServer` names a server:

<!-- snippet: use-mcp-server -->
<a id='snippet-use-mcp-server'></a>
```cs
UseMcpServer("status_page");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/SupportAgent.cs#L27-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-mcp-server' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The host connects it, with the MCP client of its choice, by implementing `IMcpToolProvider`:

<!-- snippet: mcp-tool-provider -->
<a id='snippet-mcp-tool-provider'></a>
```cs
internal sealed class McpToolProvider : IMcpToolProvider
{
    public async ValueTask<IReadOnlyList<AITool>> GetToolsAsync(string server, CancellationToken ct)
    {
        var transport = new HttpClientTransport(new() { Endpoint = new Uri($"https://mcp.example.com/{server}") });
        var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

        return [.. await client.ListToolsAsync(cancellationToken: ct)];
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/McpToolProvider.cs#L6-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-tool-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register it with `WithMcpToolProvider<T>()`. It is resolved from the run's services, so it may connect each customer
with their own credentials. `UseMcpServer(...).RequiresApproval()` asks the host before every call of the server's
tools; see [tool approval](./tool-approval.md). A server tool named like one of the agent's own tools fails the run.

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/StatusAgent.cs#L9-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-hosted-mcp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
