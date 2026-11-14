---
title: MCP servers
description: Offer the tools of MCP servers to an agent, connected by your app's own MCP client or by the model's provider.
---

# MCP servers

An agent can offer the tools of a [Model Context Protocol](https://modelcontextprotocol.io) (MCP) server next to its
toolsets. Typeshift takes no MCP dependency, so you pick the client.

## Servers your app connects

`UseMcpServer` names a server in the agent's constructor. The name is a key you choose, and your app decides which
server it stands for:

<!-- snippet: use-mcp-server -->
<a id='snippet-use-mcp-server'></a>
```cs
UseMcpServer("status_page")
    .ReadOnly();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L33-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-use-mcp-server' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Your app connects the server by implementing `IMcpToolProvider`. Its `GetToolsAsync` receives the name and returns
that server's tools. The provider below uses the official client, so add its package first:

```bash
dotnet add package ModelContextProtocol.Core
```

`McpClient` and `HttpClientTransport` are in the namespace `ModelContextProtocol.Client`. The provider builds the
server's URL from the name:

<!-- snippet: mcp-tool-provider -->
<a id='snippet-mcp-tool-provider'></a>
```cs
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
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/McpToolProvider.cs#L7-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-tool-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Registering the provider

Register the provider with `WithMcpToolProvider<T>()`:

<!-- snippet: mcp-registration -->
<a id='snippet-mcp-registration'></a>
```cs
services.AddTypeshift()
    .WithMcpToolProvider<McpToolProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L89-L92' title='Snippet source file'>snippet source</a> | <a href='#snippet-mcp-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The provider is scoped, and Typeshift resolves it from the run's services, so each scope, such as a web request, gets
its own. That lets it connect each customer with their own credentials. When the scope ends, it disposes the provider
along with its connections, so the provider above opens one connection per server for each scope.

Typeshift calls `GetToolsAsync` for each server of an agent whenever it resolves that agent's options. That happens
once for every run, which includes each call of the agent as a [sub-agent](./multi-agent.md#sub-agents) and each run
it answers after a [handoff](./multi-agent.md#handoff), and once for every `ResolveOptionsAsync` of the
[raw path](./running-agents.md#the-raw-path). With the provider above, a web request therefore connects the server
and lists its tools before its first call to the model. For a server that every customer reaches with the same
credentials, keep the connection or the tool list in a singleton service that your provider reads, so that only the
first request pays for it.

Sub-agents that work at the same time share their caller's scope, and with it the provider, so two calls of
`GetToolsAsync` can overlap. The provider above keeps its clients in a `ConcurrentDictionary` for that reason.

## How the agent offers the tools

In the tool list the model receives, the servers' tools come after the agent's own.
`UseMcpServer(...).RequiresApproval()` makes every call of the server's tools need approval, as
[Approval and modes](./tool-approval.md) describes. The decision about such a call names the server in `Server`, and
an [approval for the conversation](./tool-approval.md#approving-for-the-conversation) covers that server's tool
only. `ReadOnly()` marks every tool of the server as reading only, so
[plan mode](./tool-approval.md#plan-mode) offers them all. Use it only for a server whose tools change nothing.

The run fails when a server's tool is named like one of the agent's own tools or like a tool of another server it
uses, because the model would otherwise see only one of them. The error names the server and the tool. It also fails
when a server's tool has a name the run offers itself: `ask_user`, `exit_plan_mode`, or one that starts with
`handoff_to_` ([TSH035](./diagnostics.md#tsh035) lists them). A server picks its tools' names, so check what a
server you do not control offers. An agent that uses servers also fails its run when no provider is registered.

A server's tool whose call throws, for example because the server is down, fails like any other tool. The model
reads only that the call failed, and the exception is in your log and on the call's span
([When a tool fails](./toolsets.md#when-a-tool-fails)).

::: danger A server's tools are untrusted
Treat a server's tool descriptions and results as untrusted text, never as instructions. The model reads both, so a
server you do not control puts its words into the model's context.
:::

## Servers the model provider connects

Some model providers call MCP servers themselves. For those, put Microsoft.Extensions.AI's `HostedMcpServerTool` in
the chat options:

<!-- snippet: hosted-mcp -->
<a id='snippet-hosted-mcp'></a>
```cs
WithChatOptions()
    .WithModel("anthropic", "claude-sonnet-5-5")
    .WithTools(new HostedMcpServerTool("status_page", new Uri("https://mcp.example.com/status")));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/StatusAgent.cs#L9-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-hosted-mcp' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The model provider then connects to the server itself, and your `IMcpToolProvider` takes no part. A hosted tool
carries no `ReadOnly()` mark, so plan mode hides it.

::: warning Only some clients send a hosted server
The `IChatClient` that serves the run has to send the server to the provider, and only a client of an API that
connects MCP servers itself does. With Microsoft.Extensions.AI.OpenAI 10.10.0 and Anthropic 12.55.0, two clients
send it: OpenAI's Responses client, shown below, and Anthropic's beta client, `client.Beta.AsIChatClient(model)`.
OpenAI's Chat Completions client, `GetChatClient(model).AsIChatClient()`, and Anthropic's default
`client.AsIChatClient(model)` drop the tool without an error, so the model never sees the server's tools.
:::

<!-- snippet: openai-responses-client -->
<a id='snippet-openai-responses-client'></a>
```cs
#pragma warning disable OPENAI001
var client = new OpenAIClient(apiKey)
    .GetResponsesClient()
    .AsIChatClient("gpt-5-mini");
#pragma warning restore OPENAI001
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L138-L144' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-responses-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With Microsoft.Extensions.AI.OpenAI 10.10.0, the Responses client is still marked experimental: `GetResponsesClient()`
and its `AsIChatClient(model)` fail the build with OPENAI001 until you suppress it, as the two `#pragma` lines do.

## Related

- [Approval and modes](./tool-approval.md): asking before a server's tools run.
- [Plan mode](./tool-approval.md#plan-mode): which tools plan mode offers.
- [Agents](./defining-agents.md#selecting-tools): offering toolsets next to servers.
- [Builders](./builders.md): `UseMcpServer` among the other calls of an agent's constructor.
