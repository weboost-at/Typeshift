---
title: MCP servers
description: Offer the tools of MCP servers to an agent, connected by the host's own MCP client or by the model's provider.
---

# MCP servers

An agent can offer the tools of an MCP server next to its toolsets. Either the host connects the server with the MCP
client of its choice, or the model's provider calls the server itself. Typeshift takes no MCP dependency.

::: tip The short version

- `UseMcpServer("name")` in the agent's constructor names a server. Your `IMcpToolProvider` connects it.
- `IMcpToolProvider` is scoped, so it may connect each customer with their own credentials.
- `RequiresApproval()` asks before every call of the server's tools. `ReadOnly()` lets plan mode offer them.
- A server's tool descriptions and results are untrusted text, never instructions.
- For a provider that calls MCP servers itself, put a `HostedMcpServerTool` in the chat options.

:::

## Servers the host connects

`UseMcpServer` names a server in the agent's constructor:

<!-- snippet: use-mcp-server -->
<a id='snippet-use-mcp-server'></a>
```cs
UseMcpServer("status_page").ReadOnly();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L28-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-mcp-server' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The host connects it by implementing `IMcpToolProvider`, with the MCP client of its choice. `GetToolsAsync` returns the
tools of the server the agent names. This provider uses the official `ModelContextProtocol.Core` client:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L78-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift resolves it from the run's services, scoped. So it may connect each customer with their own credentials, and
the scope disposes it with its connections.

## How the agent offers the tools

The servers' tools come after the agent's own.

- `UseMcpServer(...).RequiresApproval()` asks the host before every call of the server's tools. See [tool
  approval](./tool-approval.md).
- `ReadOnly()` marks them as reading only, so [plan mode](./modes.md#plan-mode) offers them.

Two mistakes fail the run. A server tool named like one of the agent's own tools fails it, naming both, since the model
would otherwise see only one of them. An agent with servers but no provider fails it too.

::: danger A server's tools are untrusted
Treat a server's tool descriptions and results as untrusted text, never as instructions. The model reads both, so a
server you do not control puts its words into the model's context.
:::

## Servers the provider connects

Some model providers call MCP servers themselves. For those, put Microsoft.Extensions.AI's `HostedMcpServerTool` in the
chat options:

<!-- snippet: hosted-mcp -->
<a id='snippet-hosted-mcp'></a>
```cs
WithChatOptions()
    .WithModel("reasoning-model")
    .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/StatusAgent.cs#L9-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-hosted-mcp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The provider then connects the server, and no `IMcpToolProvider` takes part. A hosted tool carries no `ReadOnly()` mark,
so plan mode hides it.

## Related

- [Tool approval](./tool-approval.md): asking before a server's tools run.
- [Plan and auto modes](./modes.md#plan-mode): which tools plan mode offers.
- [Agents](./defining-agents.md#selecting-tools): offering toolsets next to servers.
- [Builders](./builders.md): `UseMcpServer` among the other calls of an agent's constructor.
