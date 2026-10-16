---
title: MCP servers
description: Offer the tools of MCP servers to an agent, connected by the host's own MCP client or by the model's provider.
---

# MCP servers

An agent can offer an MCP server's tools next to its toolsets. Typeshift takes no MCP dependency, so you pick the
client.

## Servers the host connects

`UseMcpServer` names a server in the agent's constructor:

<!-- snippet: use-mcp-server -->
<a id='snippet-use-mcp-server'></a>
```cs
UseMcpServer("status_page")
    .ReadOnly();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L31-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-mcp-server' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The host connects it by implementing `IMcpToolProvider`, whose `GetToolsAsync` returns the tools of the server the
agent names. This provider uses the official `ModelContextProtocol.Core` client:

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

## Registering the provider

Register the provider with `WithMcpToolProvider<T>()`:

<!-- snippet: mcp-registration -->
<a id='snippet-mcp-registration'></a>
```cs
services.AddTypeshift()
    .WithMcpToolProvider<McpToolProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L79-L82' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift resolves it from the run's services, scoped. That lets it connect each customer with their own
credentials, and the scope disposes it along with its connections.

## How the agent offers the tools

The servers' tools come after the agent's own. `UseMcpServer(...).RequiresApproval()` asks the host before every call
of the server's tools, as [tool approval](./tool-approval.md) describes. `ReadOnly()` marks them as reading only, so
[plan mode](./tool-approval.md#plan-mode) offers them.

Two mistakes fail the run. A server tool named like one of the agent's own tools fails it, naming both, because the
model would otherwise see only one of them. An agent with servers but no registered provider fails it too.

::: danger A server's tools are untrusted
Treat a server's tool descriptions and results as untrusted text, never as instructions. The model reads both, so a
server you do not control puts its words into the model's context.
:::

## Servers the provider connects

Some model providers call MCP servers themselves. For those, put Microsoft.Extensions.AI's `HostedMcpServerTool` in
the chat options:

<!-- snippet: hosted-mcp -->
<a id='snippet-hosted-mcp'></a>
```cs
WithChatOptions()
    .WithModel("reasoning-model")
    .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/StatusAgent.cs#L9-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-hosted-mcp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The provider then connects the server, and no `IMcpToolProvider` takes part. A hosted tool carries no `ReadOnly()`
mark, so plan mode hides it.

## Related

- [Tool approval](./tool-approval.md): asking before a server's tools run.
- [Plan and auto modes](./tool-approval.md#plan-mode): which tools plan mode offers.
- [Agents](./defining-agents.md#selecting-tools): offering toolsets next to servers.
- [Builders](./builders.md): `UseMcpServer` among the other calls of an agent's constructor.
