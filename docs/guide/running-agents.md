---
title: Running an agent
description: Run an agent through its host with stored settings, run settings, typed output or streaming, or call its chat client yourself.
---

# Running an agent

You run an agent through its host, `AgentHost<TAgent>`: pass the user's message, and get back the model's answer with
the tool calls it made on the way. A run can apply a customer's stored settings, change them for one run, ask for a
typed answer, or stream as the model writes. [Using an agent](./using-an-agent.md) shows where your app gets the
host.

## Registration

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
services.AddTypeshift()
    .WithStore<EfTypeshiftStore>()
    .WithRunMiddleware<BlockedTopics>()
    .WithToolMiddleware<AuditToolCalls>();

services.AddKeyedChatClient("openai", fast);
services.AddKeyedChatClient("anthropic", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers the toolsets and agents of this project and the projects it references, plus a host per
agent. `fast` and `reasoning` stand for the `IChatClient` instances your app creates, one per provider, registered
under the provider's name. `WithStore<T>()` registers the store that keeps settings and conversations.
[Registration](./registration.md#the-chain) covers the rest of the chain.

## Chat clients

A run takes the `IChatClient` registered under its provider, or the one registered for every agent, from the run's
services. [Models and providers](./models.md) shows how to register them, how an agent names its model, how to
create [a real client](./models.md#a-real-client) and when to write a provider of your own.

## Run an agent

<!-- snippet: run -->
<a id='snippet-run'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var answer = await support.RunAsync("Where is my refund?");

Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run returns Microsoft.Extensions.AI's `ChatResponse`: the model's messages, with the tool calls and results between
them. Each message names the agent that wrote it in `AuthorName`.

[How a run works](./how-a-run-works.md) follows the steps every run passes through, streaming or not, from resolving
its options to the model's answer.

## Stored settings

Give the host a settings id with `WithSettingsId`, and its runs layer the [settings stored](./settings-and-stores.md)
under it over the agent's code and its own stored settings. The call returns a copy of the host, as
[Ids of your own types](./settings-and-stores.md#ids-of-your-own-types) explains:

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L34-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-with-settings-id' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run settings

A run can also pass settings of its own, as an instance of the agent's generated
[`Settings`](./generated-members.md#settings) record. They override the stored settings for that run only:

<!-- snippet: run-settings -->
<a id='snippet-run-settings'></a>
```cs
var run = new SupportAgent.Settings
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Temperature = 0,
    SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
    SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
};

var thorough = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?", settings: run);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L42-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values the run leaves null keep what the stored settings or the agent's code set, as
[settings layering](./settings-and-stores.md#settings-layering) explains.

## Conversations

Give the host a conversation id with `WithConversationId`, and its runs continue that conversation and store each of
their steps in it. If a crash cuts a run off, `ResumeAsync` continues it. [Conversations](./conversations.md) covers
both.

## Structured output

`RunAsync<T>` asks the model for JSON in `T`'s schema and returns it typed, in `Result`:

<!-- snippet: structured-output -->
<a id='snippet-structured-output'></a>
```cs
var report = await support.WithSettingsId(settingsId).RunAsync<RefundStatus>("Summarize the refund.");

Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L57-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L100-L109' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`T`'s XML docs describe its properties, as they describe the types tools take
([XML docs of parameter types](./registration.md#xml-docs-of-parameter-types)). Providers take only an object with
properties as the schema of an answer. So a `T` of another shape, such as a number, an enum, a list, a dictionary, a
polymorphic type or a [union](./toolsets.md#union-types), is asked for under a `data` property, and `Result` reads it
from there. Under
[Native AOT](./native-aot.md), `T` needs JSON metadata.

A member that System.Text.Json cannot read or write, such as an `Exception`, whose `TargetSite` is a `MethodBase`,
fails the run before it calls the model, naming the member. Leave such a member out of `T`, or mark it
`[JsonIgnore]`, since the model could not send one anyway. A tool's parameters fail the same way.

`RunStreamingAsync<T>` streams that answer as the model writes it. Read its updates to the end, and its `Response`
holds the result:

<!-- snippet: structured-streaming -->
<a id='snippet-structured-streaming'></a>
```cs
var summary = support.WithSettingsId(settingsId).RunStreamingAsync<RefundStatus>("Summarize the refund.");

await foreach (var update in summary)
    Console.Write(update.Text);

Console.WriteLine($" ({summary.Response.Result.State})");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L65-L72' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The updates carry the model's JSON as it writes it, under `data` for a `T` that is not an object, so show the user
`Result` rather than the streamed text. The model is called when you read the updates, and they can be read once: a
second `await foreach` throws an `InvalidOperationException`. `Response` throws one until the updates are read to the
end, and keeps throwing when the run failed, was cancelled or was left early.

Both typed runs also take a `ChatMessage`, so the run that answers an [approval](./tool-approval.md) asks for `T`
again. An agent the run is [handed off](./multi-agent.md) to answers in `T` too.

## Streaming

`RunStreamingAsync` returns the model's updates as they arrive. When the model calls tools, the tools run between its
updates, and the stream goes on with the model's next response:

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.WithSettingsId(settingsId).RunStreamingAsync("Where is my refund?"))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L76-L79' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Anyone [watching the conversation](./conversations.md#live-updates) sees the updates too.

## The raw path

If your app needs the `IChatClient` itself, for example to hand it to a library that takes one, split a run into
three steps: resolve the options, create the client, and call the client with those options. Here `run` is the
`SupportAgent.Settings` from [Run settings](#run-settings):

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync(settings: run);

using var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L83-L88' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The client is the run's whole pipeline, the tool loop and the agent's run options included. It serves concurrent
calls, each with the limits of its own options. Disposing it does not dispose the client your provider returned.

To run in a conversation, resolve the options on a host with a conversation id, such as
`support.WithConversationId(chat).ResolveOptionsAsync()`, and every call with those options continues and stores that
conversation. On a host without one, `ChatOptions.ConversationId` names the conversation instead. It is a string, so
it names a conversation in your `IConversationStore<string>`, which suits a library that sets it itself. Typeshift
never sends the conversation's id to the provider.

## Related

- [Using an agent](./using-an-agent.md): getting the host in a web app, a console app or a background job.
- [How a run works](./how-a-run-works.md): the steps every run passes through, and what each one can change.
- [Models and providers](./models.md): the chat client provider and how agents name their models.
- [Agent host](./generated-members.md#the-agent-host): every member of the host and its signature.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Conversations](./conversations.md): storing, resuming and steering runs.
- [Serving a chat endpoint](./chat-endpoint.md): runs, streaming and approvals behind ASP.NET Core endpoints.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Architecture](../contributing/architecture.md#a-run): the same pipeline from the code's side.
