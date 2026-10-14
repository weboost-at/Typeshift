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
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedSingleton("fast-model", fast);
services.AddKeyedSingleton("reasoning-model", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers every toolset and agent of the project and of the projects it references, with a host
per agent. `WithChatClientProvider<T>()` maps model names to clients, `WithStore<T>()` turns persistence on (see
[settings and stores](./settings-and-stores.md)), and `WithRealtimeTracking()` publishes the updates of streaming runs
to watchers (see [live updates](./conversations.md#live-updates)). [Registration](./registration.md) lists what the
call registers and every `With...`.

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/KeyedChatClientProvider.cs#L6-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

It is resolved from the run's services, so it may pick a customer's own credentials. Typeshift never disposes the
client it returns.

## The run's services

`AgentHost<TAgent>` is transient: it runs with the services of the scope it was resolved from. Tools and the chat
client provider resolve from that scope, so a request's scoped services, the current customer say, reach them.
Resolve it from a request's or a job's scope:

<!-- snippet: run -->
<a id='snippet-run'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var answer = await support.RunAsync("Where is my refund?");

Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L13-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run returns Microsoft.Extensions.AI's `ChatResponse`: the model's messages and the tool calls and results between
them, each carrying the agent's name as `AuthorName`.

## Conversations

With a conversation id, the run resumes the conversation and stores itself in it, step by step; see
[conversations](./conversations.md). `ResumeAsync` continues a run a crash cut off.

## Stored settings

With a settings id, the run layers the settings stored under it over the agent's code; see
[settings and stores](./settings-and-stores.md):

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.RunAsync("Where is my refund?", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L22-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-with-settings-id' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run settings

A run can override what the agent's `Settings` allow, over the stored settings, for that run only:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L30-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values a run leaves null keep what the stored settings or the agent's code set; see
[settings layering](./settings-layering.md).

## Structured output

`RunAsync<T>` asks the model for JSON of `T`'s schema and returns it typed in `Result`:

<!-- snippet: structured-output -->
<a id='snippet-structured-output'></a>
```cs
var report = await support.RunAsync<RefundStatus>("Summarize the refund.", settingsId);

Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L44-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L76-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Streaming

`RunStreamingAsync` streams the updates; tool calls run between them.

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.RunStreamingAsync("Where is my refund?", settingsId))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L52-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The raw path

For a host that needs the `IChatClient` itself, a run splits into three steps: resolve the options, create the client,
call it with those options. The client is the run's pipeline, the tool loop with the agent's run options included;
disposing it leaves the provider's client alone. `ChatOptions.ConversationId` names the conversation there, as a
string of the build's id type; the provider never receives it.

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.ResolveOptionsAsync(settingsId, run);

using var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L59-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
