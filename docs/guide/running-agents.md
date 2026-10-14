---
title: Running agents
description: Run an agent through its host, with stored settings, run settings, typed output or streaming.
---

# Running agents

You run an agent through its host, `AgentHost<TAgent>`, which `AddTypeshift()` registers for every agent. Resolve the
host from a scope and call `RunAsync`: the tools see that scope's services. A run can use stored settings, override them
for itself, store a conversation, return typed output or stream. [How a run works](./how-a-run-works.md) shows the steps
every run passes through.

::: tip The short version

- Resolve `AgentHost<TAgent>` from a request's scope, so the tools and the chat client provider see that request's
  services.
- Your `IChatClientProvider` returns the `IChatClient` for the model an agent names.
- Pass a settings id to layer the stored settings, and a conversation id to store the run. Run `Settings` override the
  stored settings for one run.
- `RunAsync<T>` returns typed output. `RunStreamingAsync` streams the updates.
- The raw path gives you the run's `IChatClient` and options, for a host that needs the client itself.

:::

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

`AddTypeshift()` registers the toolsets and agents of the project and the projects it references, and a host per agent.
The chain registers the rest. This page uses two of its calls: `WithChatClientProvider<T>()`, which maps model names to
clients, and `WithStore<T>()`, which persists settings and conversations. [Registration](./registration.md#the-chain)
explains every call.

## Chat clients

`IChatClientProvider` returns the `IChatClient` of a model name. Provider packages register their clients with DI, so a
provider over keyed registrations covers most hosts:

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

The registration above adds the clients of the two models as keyed singletons. Typeshift resolves the provider from the
run's services, so it may pick a customer's own credentials. Typeshift never disposes the client it returns.

## The run's services

`AgentHost<TAgent>` is transient: it runs with the services of the scope it was resolved from. Tools and the chat client
provider resolve from that scope, so a request's scoped services, such as the current customer, reach them. Resolve the
host from the scope of a request or a job:

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
them. Each message carries the name of the agent that wrote it as `AuthorName`.

## Stored settings

With a settings id, the run layers the settings stored under it over the agent's code; see [Settings and
stores](./settings-and-stores.md):

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.RunAsync("Where is my refund?", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L22-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-with-settings-id' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run settings

A run can pass the agent's `Settings` for itself. They override the stored settings, for that run only:

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

Values a run leaves null keep what the stored settings or the agent's code set; see [Settings
layering](./settings-layering.md).

## Conversations

With a conversation id, the run resumes the conversation and stores itself in it, step by step. `ResumeAsync` continues
a run a crash cut off. See [Conversations](./conversations.md).

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

Under Native AOT, `T` needs JSON metadata; see [Native AOT](./native-aot.md).

## Streaming

`RunStreamingAsync` streams the updates, and tool calls run between them:

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.RunStreamingAsync("Where is my refund?", settingsId))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L52-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With `WithRealtimeTracking()`, the updates also reach anyone watching the conversation; see [live
updates](./conversations.md#live-updates).

## The raw path

A host that needs the `IChatClient` itself splits a run into three steps: resolve the options, create the client, and
call it with those options:

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.ResolveOptionsAsync(settingsId, run);

using var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L59-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The client is the run's pipeline: the tool loop, with the agent's run options included.
- Disposing it leaves the provider's client alone.
- `ChatOptions.ConversationId` names the conversation to run in, as a string of the build's id type. The provider never
  receives it.

## Related

- [How a run works](./how-a-run-works.md): the steps every run passes through.
- [Agent host](./agent-host.md): every member of the host and its signature.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Conversations](./conversations.md): storing, resuming and steering runs.
- [Settings layering](./settings-layering.md): where each of a run's values comes from.
