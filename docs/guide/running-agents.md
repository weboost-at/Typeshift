---
title: Running agents
description: Run an agent through its host with stored settings, run settings, typed output or streaming, and the steps every run passes through.
---

# Running agents

You run an agent through its host, `AgentHost<TAgent>`, which your app takes from the scope of the request or job that
runs the agent ([Using an agent](./using-an-agent.md)).

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

services.AddKeyedSingleton("openai", fast);
services.AddKeyedSingleton("anthropic", reasoning);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L18-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers the toolsets and agents of this project and the projects it references, plus a host per
agent. `fast` and `reasoning` stand for the `IChatClient` instances your app creates, one per provider. This page
needs two calls of the chain: `WithChatClientProvider<T>()` registers your class that returns the client of a
provider's model, and `WithStore<T>()` registers the store that keeps settings and conversations.
[Registration](./registration.md#the-chain) covers the rest.

## Chat clients

Your `IChatClientProvider` returns the `IChatClient` for a provider and a model, and Typeshift resolves it from the
run's services. [Models and providers](./models.md) shows how to write one, how an agent names its model and how to
create [a real client](./models.md#a-real-client).

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

## Stored settings

Pass a settings id, and the run layers the [settings stored](./settings-and-stores.md) under it over the agent's code:

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.RunAsync("Where is my refund?", settingsId);
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

var thorough = await support.RunAsync("Where is my refund?", settingsId, settings: run);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L42-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values the run leaves null keep what the stored settings or the agent's code set, as
[settings layering](./settings-and-stores.md#settings-layering) explains.

## Conversations

Pass a conversation id, and the run continues that conversation and stores each of its steps in it. If a crash cuts a
run off, `ResumeAsync` continues it. [Conversations](./conversations.md) covers both.

## Structured output

`RunAsync<T>` asks the model for JSON in `T`'s schema and returns it typed, in `Result`:

<!-- snippet: structured-output -->
<a id='snippet-structured-output'></a>
```cs
var report = await support.RunAsync<RefundStatus>("Summarize the refund.", settingsId);

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L89-L98' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`T`'s schema leaves out XML docs, which describe only tool parameters. Describe its properties with `[Description]`
attributes when the model needs to know more than their names. Under [Native AOT](./production.md#native-aot), `T`
needs JSON metadata.

## Streaming

`RunStreamingAsync` returns the model's updates as they arrive. When the model calls tools, the tools run between its
updates, and the stream goes on with the model's next response:

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.RunStreamingAsync("Where is my refund?", settingsId))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L65-L68' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With `WithRealtimeTracking()`, anyone [watching the conversation](./conversations.md#live-updates) sees the updates
too.

## The raw path

If your app needs the `IChatClient` itself, for example to hand it to a library that takes one, split a run into
three steps: resolve the options, create the client, and call the client with those options. Here `run` is the
`SupportAgent.Settings` from [Run settings](#run-settings):

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.ResolveOptionsAsync(settingsId, run);

using var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L72-L77' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The client is the run's whole pipeline, the tool loop and the agent's run options included. It serves concurrent
calls, each with the limits of its own options. Disposing it does not dispose the client your provider returned. To
run in a conversation, set `ChatOptions.ConversationId` on the options to the conversation's id, written as a string
of the build's id type. Typeshift reads the conversation from it and never sends it to the provider.

## How a run works

A run is one call of `RunAsync`, `RunAsync<T>`, `RunStreamingAsync`, `ResumeAsync` or `ResumeStreamingAsync`, or of a
client from the [raw path](#the-raw-path). Streaming or not, every run passes through the same steps in the same
order, and that order tells you what your middleware sees, when context is added, which calls ask for approval and
what is stored when.

```mermaid
flowchart TD
  start(["RunAsync(input, settingsId, conversationId, settings)"]) --> resolve[Resolve the options]
  resolve --> conversation[Resume the conversation]
  conversation --> middleware[Run middleware]
  middleware --> context[Context providers]
  context --> approval[Approval and mode]
  approval --> before
  subgraph loop [Tool loop]
    before[Store new steps, then compact] --> model[Call the model]
    model -->|tool calls| tools[Tool middleware, then the tools]
    tools --> before
  end
  model -->|an answer| done([ChatResponse])
```

Each step wraps the steps below it, so the answer passes back up through them in reverse order: run middleware sees
the response before the conversation stores how the run ended.

1. **Resolve the options.** The host builds the run's `ChatOptions` from the agent's code, then the settings stored
   under the settings id, then the run's own settings. It creates the tools the agent offers, their settings resolved
   through the same layers, and adds the sub-agents and the tools of MCP servers after them. The layers resolve once,
   when the run starts ([settings layering](./settings-and-stores.md#settings-layering)).
2. **Resume the conversation.** With a conversation id, the run puts the stored entries ahead of the new input, so the
   model sees the whole conversation, and stores the input. It keeps the conversation's mode unless the run's settings
   name one. If an earlier run [handed the conversation off](./multi-agent.md#handoff) to another agent, the agent
   that answered last answers this run too. Without a conversation id, the run starts fresh and stores nothing. Within
   one process, a conversation has one run at a time, and starting a second while the first goes on throws
   ([Conversations](./conversations.md)).
3. **Run middleware.** Each `IRunMiddleware` wraps the rest of the run ([Middleware](./middleware.md)).
4. **Context providers.** Each context provider the agent uses can add instructions, messages and tools to this run.
   The conversation stores none of it, so every run calls the providers again
   ([Context providers](./middleware.md#context-providers)).
5. **Approval and mode.** If the input carries your app's answers to the approval requests the conversation's last
   run left open, the run matches each answer to its request. In plan mode, it offers the model only the read-only tools and
   `exit_plan_mode`. After each pass of the tool loop, approval rules, standing approvals and auto mode decide every
   call they can. If calls remain that only your app can decide, the run stores their requests and ends
   ([Approval and modes](./tool-approval.md)).
6. **The tool loop.** Microsoft.Extensions.AI's `FunctionInvokingChatClient` calls the model, runs the tools it asks
   for, each through the tool middleware, and calls the model again until it answers. The
   [run options](./defining-agents.md#run-options) set its limits, such as the most rounds of tool calls.

Before every model call inside the loop, the run appends the new steps to the conversation: tool results, input your
app [steered](./conversations.md#steering) into the run, and the answers of sub-agents running
[in the background](./multi-agent.md#in-the-background). Each model response is stored as it arrives, so a crash
loses at most the calls in flight.

When compaction is on and a call would exceed the token limit, the run compacts what the model sees, while the stored
conversation keeps everything ([compaction](./conversations.md#compaction)). Typeshift traces every call to the
provider's client ([Observability](./production.md#observability)).

### What each step can change

| Step | Sees | Can |
|---|---|---|
| Run middleware | The agent, the messages, the options and the mode | Change the messages and options of this run, or answer in place of the model |
| Context provider | The run | Add instructions, messages and tools to this run |
| Approval rule | Each call that requires approval, with the arguments the model sent | Approve it, reject it with a reason for the model, or abstain |
| Tool middleware | Each tool call: the function, its arguments, the messages so far | Answer in place of the tool, or end the tool loop |

## Related

- [Using an agent](./using-an-agent.md): getting the host in a web app, a console app or a background job.
- [Models and providers](./models.md): the chat client provider and how agents name their models.
- [Agent host](./generated-members.md#the-agent-host): every member of the host and its signature.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Conversations](./conversations.md): storing, resuming and steering runs.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Architecture](../contributing/architecture.md#a-run): the same pipeline from the code's side.
