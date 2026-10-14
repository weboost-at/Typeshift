# Running agents

A host runs an agent through its `AgentHost<TAgent>`, which `AddTypeshift()` registers per agent.

## Registration

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>()
    .WithRealtimeTracking()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>()
    .WithMcpToolProvider<McpToolProvider>();

services.AddKeyedSingleton("fast-model", fast);
services.AddKeyedSingleton("reasoning-model", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Registration.cs#L15-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithStore<T>()`** turns persistence on; see [settings and stores](./settings-and-stores.md).
- **`WithChatClientProvider<T>()`** maps model names to clients.
- The rest is optional: [live updates](./conversations.md#live-updates), [middleware](./middleware.md),
  [MCP servers](./mcp.md), the services of the [built-in toolsets](./built-in-toolsets.md).

## Chat clients

`IChatClientProvider` returns the `IChatClient` of a model name. Provider packages register their clients with DI, so
a provider over keyed registrations covers most hosts:

<!-- snippet: chat-client-provider -->
<a id='snippet-chat-client-provider'></a>
```cs
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult(services.GetRequiredKeyedService<IChatClient>(model));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/KeyedChatClientProvider.cs#L6-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

It is resolved from the run's services, so it may pick a customer's own credentials. Typeshift never disposes the
client it returns.

## The run's services

`AgentHost<TAgent>` is transient: it runs with the services of the scope it was resolved from. Tools, context
providers, middleware and the chat client provider all resolve from that scope, so a request's scoped services, the
current customer say, reach them. Resolve it from a request's or a job's scope:

<!-- snippet: run -->
<a id='snippet-run'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var answer = await support.RunAsync("Where is my refund?");

Console.WriteLine(answer.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L13-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Stored settings

With a settings id, the run layers the settings stored under it over the agent's code:

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.RunAsync("Where is my refund?", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L22-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-with-settings-id' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run settings

A run can override what the agent's `Settings` allow, for that run only:

<!-- snippet: run-settings -->
<a id='snippet-run-settings'></a>
```cs
var run = new SupportAgent.Settings
{
    Model = "reasoning-model",
    Temperature = 0,
    SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
    SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
};

var thorough = await support.RunAsync("Where is my refund?", settingsId, settings: run);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L28-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values a run leaves null keep what the layers below set; see [the layers](./tool-settings.md#the-layers).

## Structured output

`RunAsync<T>` asks the model for JSON of `T`'s schema and returns it typed in `Result`:

<!-- snippet: structured-output -->
<a id='snippet-structured-output'></a>
```cs
var report = await support.RunAsync<RefundStatus>("Summarize the refund.", settingsId);

Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L40-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: structured-output-type -->
<a id='snippet-structured-output-type'></a>
```cs
internal sealed record RefundStatus(string Invoice, RefundState State, DateOnly? Expected);

internal enum RefundState
{
    Requested,
    Approved,
    Paid
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L61-L70' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Streaming

`RunStreamingAsync` streams the updates; tool calls run between them. The exchange is stored when the stream ends,
fails, or the consumer stops early.

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.RunStreamingAsync("Where is my refund?", settingsId))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L46-L49' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The raw path

For a host that needs the `IChatClient` itself, the run splits into three steps: resolve the options, create the
client, call it with those options. The conversation id is a string here, parsed into the build's id type.

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.ResolveOptionsAsync(settingsId, run);
options.ConversationId = "7";

var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/RunningAgents.cs#L51-L57' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
