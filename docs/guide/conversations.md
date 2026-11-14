---
title: Conversations
description: Store a conversation under an id, resume it after a crash, steer and watch a run, and compact a long one.
---

# Conversations

Give the host a conversation id, and Typeshift stores every step of its runs under it. The next run picks up where
the last one ended, even after a crash.

## Tracking a conversation

<!-- snippet: conversation -->
<a id='snippet-conversation'></a>
```cs
var conversationId = 7;
var chat = support.WithConversationId(conversationId);

await chat.RunAsync("Where is my refund?");
await chat.RunAsync("When will it arrive?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L15-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`chat` is the host with conversation 7 ([Ids of your own types](./settings-and-stores.md#ids-of-your-own-types)). The
first run starts the conversation and the second resumes it. Typeshift puts the stored messages ahead of the new input,
so the model sees the whole conversation. Because Typeshift sends the history itself, it never passes the conversation
id on to the provider.

Settings ids and conversation ids are independent, and a host may have either or both. A conversation does not
remember a settings id, so every run of it that should use the stored settings needs a host with both, the call that
answers a decision included: `support.WithSettingsId(42).WithConversationId(conversationId)`.

## Conversation ids

You pick a conversation id when a chat starts, usually the key of your own record of the chat, such as a row in a
`Chats` table, and give it to the host of every run of that chat. Its type is up to you: it picks the conversation
store of that type ([Ids of your own types](./settings-and-stores.md#ids-of-your-own-types)). With no record of your
own, `Guid.CreateVersion7()` gives a new chat its id. The response's `ConversationId` names the conversation a run
was stored in, as text.

The store keeps a conversation's entries under its id, and the [tracker](#live-runs) of the id's type finds a running
run by it to steer or watch it. Without a conversation id, and without
[`PersistAllConversations`](#keeping-every-conversation), a run stores nothing, so the next one starts from scratch. A
tool call that needs your approval makes it throw, it offers no handoffs, and no tracker sees it.

### Keeping every conversation

To keep the runs of a host without a conversation id as well, turn on `PersistAllConversations`:
`AddTypeshift().WithOptions(options => options.PersistAllConversations = true)`. Such a run then starts a
conversation under a new id, and its response's `ConversationId` names it, so the next run continues it with
`support.WithConversationId(Guid.Parse(response.ConversationId!))`. A handoff continues that conversation. A call on
the [raw path](./running-agents.md#the-raw-path) names its conversation in `ChatOptions.ConversationId`, a string, so
the conversation it starts is under a `string` id, and the next call continues it with
`options.ConversationId = response.ConversationId`.

Some runs start no conversation, even with the option on:

- A sub-agent's run, which belongs to its caller's conversation.
- A run without input: it continues an interrupted run ([Resuming](#resuming)), and a new conversation has none.
- A raw-path call whose options did not come from `ResolveOptionsAsync`, such as `ChatOptions` your code created, or
  a call with no options at all ([the raw path](./running-agents.md#the-raw-path)).

New ids are `Guid`s of version 7, which sort by the time they were made. A run of the host's members keeps its new
conversation in your `IConversationStore<Guid>`, and a raw-path call keeps it under the `Guid`'s text in your
`IConversationStore<string>`. For a conversation store of another id type, give the ids with
`options.NewConversationIds(() => ...)`; the raw path takes them when they are strings. Without a store the new
conversation fits, a run that starts one throws an `InvalidOperationException` that names `NewConversationIds`. Runs
that start together call the function at the same time, so make it thread-safe, and have it return an id no
conversation has on every call.

::: danger Check the caller before you use a conversation id
Typeshift resumes whichever conversation it is given, so deciding who may see one is your app's job. Check the caller
against your own record of the conversation, as you would for any other id from a request.
:::

## What is stored

A conversation is a list of entries, appended as they happen: each run's input, every model response with its model and
token usage, the tool results of each round of calls, and an entry that ends the run. Your store receives each entry
through `AppendConversationAsync`
([Implementing a store](./implementing-a-store.md#append-to-a-conversation)).

What a run asks your app is not among the entries. A conversation has a second, small record beside them, its
decisions: the decisions that wait, with the calls they stand for, the answers your app gave, the tools
[approved for the conversation](./tool-approval.md#approving-for-the-conversation), and the conversation's
[mode](./tool-approval.md#setting-the-mode) once something named one. Your store keeps it through
`IDecisionStore<TId>` ([Keep decisions](./implementing-a-store.md#keep-decisions)). The entries hold the model's call
and, once it ran or was rejected, its result, and never a request for approval or its answer.

Each entry is a `ConversationEntry`: the name of its agent in `Agent`, its `Messages` and the time it was stored in
`At`, plus the `Model` and `Usage` of a model response, and `EndsRun` on the entry that ends a run. `IsSummary` is true
on the entry [compaction](#compaction) appends when it summarizes, which is how your code and your store tell a summary
apart ([Start from the last summary](./implementing-a-store.md#start-from-the-last-summary)). A model response of a run
with a maximum of input tokens also has `EstimatedInputTokens`, what compaction estimated its call at.

An entry serializes itself with `Serialize()` and `ConversationEntry.Deserialize(json)`, and JSON libraries handle it as
it is. `System.Text.Json` writes and reads that same JSON, and Newtonsoft.Json writes it as a string, so a store or an
API can pass entries on with either library and no settings.

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var store = provider.GetRequiredService<IConversationStore<int>>();
var entries = await store.LoadConversationAsync(conversationId, ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L23-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Runs that fail or stop early are stored too:

- A failed run stores an entry with an `ErrorContent` that ends the run, then rethrows the exception to your code.
  The `ErrorContent` holds the exception's message as it is, which no model reads. Show your users a text of your
  own in its place, since a driver's or a provider's message may name what they should not see.
- A streaming run that fails, is cancelled, or that your code stops reading early stores the updates that had arrived.
- A cancelled run is stored too, because Typeshift stores without the run's cancellation token.
- A run cut off by a crash has no ending entry, and [resuming](#resuming) continues it.

When the store fails to save, the failure is [logged](./observability.md#logs) and recorded on the run's span, and
the run still returns its answer. The step it failed to save is missing from later runs. One step is different: a
run that asks your app throws the store's exception when it could not store the model's response or the decision,
because a prompt your app shows must be one it can answer.

Whenever a run loads the conversation, it builds what the model sees so that a provider accepts it. Each tool result
follows the message that made its call, even when the store holds input or an end of a run between the two. A call
that never got a result is left out, and so are a result whose call is missing and error messages, because providers
reject them. The approval requests and answers an earlier version of Typeshift stored among the entries are left out
too.

Some providers, such as local servers with an OpenAI-compatible API, number their tool calls per response, so a later
call can get an id the conversation holds already. Typeshift renames such a call, as `call_1_2`, before it stores or
runs it, so the stored entries, the decision that waits for the call and the tool's result all carry the new id.

## Resuming

Each model response is stored as it arrives, and tool results before the next model call, so an app that crashes
mid-run loses at most the calls in flight. `ResumeAsync` continues the interrupted run of the host's conversation from
its last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.WithSettingsId(settingsId).WithConversationId(interrupted).ResumeAsync();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L64-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ResumeAsync` also continues a conversation whose decisions your app answered with `RecordAnswersAsync`: it carries
the answers out and goes on. While decisions are still open, it calls no model and returns them
([Approving or rejecting](./tool-approval.md#approving-or-rejecting)). After your app withdrew them with
`CancelDecisionsAsync`, the model reads that the request was withdrawn and goes on
([When a decision closes](./tool-approval.md#when-a-decision-closes)).

`ResumeAsync` throws an `InvalidOperationException` on a host without a conversation id, or when the conversation's last
run ended and no answered or withdrawn decision waits. A new run with input works on an interrupted conversation too: it starts
from the interrupted run's steps and adds its own input.

After a restart, the entries tell you which conversations to resume. A run stores its ending entry only when it ends,
so the last entry that `LoadConversationAsync` returns has `EndsRun` false for a run that a crash cut off and for one
that is still going on.

With one instance of your app, nothing else can be running the conversation after a restart, so such an entry means
the run was interrupted. With several instances, or during a rolling deploy, another instance may be answering that
chat at this moment, and resuming it would call the model and its tools a second time. There, resume only when the
last entry's `At` is older than a limit your app chooses, or behind a lease of your own, because keeping to
[one run at a time](#one-run-at-a-time) across processes is up to your app.

::: warning A tool can be called twice
A call interrupted while its tool ran is left out on resume. The model decides again and may call the tool a second
time, so make tools with side effects safe to repeat. A call your app approved is the exception: no later run starts
it again, and the model reads that it is not known whether the call took effect
([When a decision closes](./tool-approval.md#when-a-decision-closes)).
:::

## One run at a time

Within a process, a conversation has one run at a time. Starting a second while the first goes on throws an
`InvalidOperationException`, so [steer](#steering) the running one or wait until it ends. A run that starts while
another is still loading the conversation waits until that run has begun. Across processes, keeping to one run per
conversation is up to your app.

## Live runs

`ConversationTracker<TId>` tells your app which conversations under ids of `TId` have a run going on in this process.
It also hands out each run's updates, so a second tab or a support dashboard can follow a chat that another request
is answering ([Live updates](#live-updates)).

Inject the tracker of your conversation ids' type, or resolve it: the container has one for every id type, a
singleton, so the docs samples, whose conversations are `int`s, take `ConversationTracker<int>`. Its `Runs` lists the
runs going on in this process under ids of that type, each with its conversation, the agent that answers, its model
and when it started:

<!-- snippet: live-sessions -->
<a id='snippet-live-sessions'></a>
```cs
var tracker = provider.GetRequiredService<ConversationTracker<int>>();

foreach (var run in tracker.Runs)
    Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L32-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-sessions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`WatchRunsAsync` tells a UI when that list changes, so a list of running conversations needs no polling. It yields the
runs going on, then each run as it starts and again as it ends, with `Ended` set, until you cancel its token.
Cancelling ends the loop with an `OperationCanceledException`, so catch it where you stop watching:

<!-- snippet: watch-runs -->
<a id='snippet-watch-runs'></a>
```cs
await foreach (var run in tracker.WatchRunsAsync(ct))
    Console.WriteLine($"Conversation {run.Conversation} is {(run.Ended ? "idle" : "running")}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L91-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-runs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: warning The tracker lists every customer's runs
`Runs` and `WatchRunsAsync` hold every run under ids of the tracker's type, whoever started it. Keep only the
conversations the caller may see before you return them from an endpoint.
:::

## Steering

`TrySteer` hands new input to a conversation's run while it works, for example when a user adds a request halfway
through. The model receives it before its next call, and the run does not end before then. The input is stored like
any other. When the run had asked your app before it took the input, its decisions expire, as they would for a new
message: the model reads that those calls did not run, and the run's response lists no decision
([When a decision closes](./tool-approval.md#when-a-decision-closes)).

`TrySteer` returns false when no run of the conversation is going on in this process, or when that run is already
ending. Start a run with the input instead:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await chat.WithSettingsId(settingsId).RunAsync(input);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L51-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To stop a run, cancel its token. It is stored up to where it stopped.

## Live updates

Typeshift publishes the updates of runs as they happen, and `WatchAsync` reads one conversation's updates until you
cancel its token, which ends the loop with an `OperationCanceledException`, as with
`WatchRunsAsync`. A streaming run publishes its updates as they stream; a run that does not stream publishes each
model response and tool result as it stores it. A browser can follow them over SignalR, as
[Follow a chat from elsewhere](./chat-endpoint.md#follow-a-chat-from-elsewhere) shows:

<!-- snippet: watch-conversation -->
<a id='snippet-watch-conversation'></a>
```cs
await foreach (var update in tracker.WatchAsync(conversationId, ct))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L76-L79' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Text, reasoning and usage updates are merged and published at most every 100 ms, and other updates go out at once. A
run never waits for a watcher, so a watcher that falls more than 256 updates behind loses the oldest ones, and
Typeshift [logs a warning](./observability.md#logs) the first time it does. You can change both limits with
`WithRealtimeTracking`:

<!-- snippet: realtime-options -->
<a id='snippet-realtime-options'></a>
```cs
services.AddTypeshift()
    .WithRealtimeTracking(new RealtimeTrackingOptions
    {
        PublishInterval = TimeSpan.FromMilliseconds(250),
        MaxPendingUpdates = 1024
    });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L60-L67' title='Snippet source file'>snippet source</a> | <a href='#snippet-realtime-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: warning Live updates stay in their process
A watcher sees only the runs of its own process. When your app runs in several processes, watch in the process that
runs the conversation.
:::

::: tip Native AOT needs the tracker of a value type registered
An app published with Native AOT that injects `ConversationTracker<Guid>`, or the tracker of another value type,
registers it with `WithConversationTracker<Guid>()` ([Trackers of value-type ids](./native-aot.md#trackers-of-value-type-ids)).
:::

## Compaction

A coding agent can make hundreds of model calls in one run, and its history soon outgrows the model's context.
Compaction shrinks the history each call sees. It is off until `WithMaxInputTokens` sets a limit, and the other
[run options](./builders.md#runoptionsbuilder) let such a run go on for long enough: here `WithMaxToolIterations(500)`
raises the limit on rounds of tool calls to 500. The agent also starts in [plan mode](./tool-approval.md#plan-mode),
which has nothing to do with compaction:

<!-- snippet: long-running-options -->
<a id='snippet-long-running-options'></a>
```cs
WithRunOptions()
    .WithMode(AgentMode.Plan)
    .WithMaxToolIterations(500)
    .WithMaxInputTokens(150_000)
    .WithSummaryModel("claude-haiku-5-5")
    .Configurable(r => r.MaxToolIterations)
    .Configurable(r => r.SummaryModel);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L20-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Before every model call, Typeshift estimates the call's input tokens at about four characters per token. It corrects
that estimate by the last call: the tokens the provider reported for it, against what Typeshift had estimated for the
call as it was sent. A run's first call takes the correction from the conversation, where each model response of a
run with a maximum is stored with its estimate in `EstimatedInputTokens`, next to its `Usage`. When the estimate
reaches the maximum, Typeshift shrinks the history to `WithTargetInputTokens`, half the maximum unless set:

1. It clears the results of the oldest tool calls the model has already read, and leaves a result alone that is no
   longer than the note that would replace it. It keeps the calls, so the model knows what it did. For a tool marked
   `ReadOnly()` the note tells the model to call the tool again if it needs the result. For any other tool it says
   only that the result was cleared, because a second call would send, pay or delete a second time.
2. If that is not enough, it summarizes the oldest turns with the [summary model](./models.md#the-summary-model), the
   run's own unless you name another. The summary model reads those turns as text: each tool call with its arguments,
   and the first 2,000 characters of each result, including the results the first step cleared. When the summary
   call fails, the run goes on: its own model writes the summary, or the call goes out without one
   ([The summary model](./models.md#the-summary-model)).

The instructions and the latest call of a tool named `write_todos`, if the agent offers one, always stay, and a cut
never separates a tool call from its result.
Every later model call of the same run starts from the history as compaction left it, with the same results cleared
and the same summary.

The messages a [context provider](./middleware.md#context-providers) added stay as they are too, and so does a message
that [run middleware](./middleware.md#wrap-every-run) added to the ones it received. The summary model never reads
them, the stored summary holds none of them, and every later call of the run still has them. A message that run
middleware put in place of another is summarized like the message it stands for.

The stored conversation keeps everything. Compaction appends an entry of its own, with `IsSummary` set to true, that
holds the summary and the messages it kept after it. Later runs start from that entry instead of summarizing again, and
read nothing before it: the entry also keeps the background agents still working. The conversation's
[mode](./tool-approval.md#setting-the-mode) and the
[approvals for the conversation](./tool-approval.md#approving-for-the-conversation) are stored with its decisions,
which no summary touches.

## Related

- [Running agents](./running-agents.md#conversations): giving a run a conversation id.
- [How a run works](./how-a-run-works.md): where a run resumes and stores its steps.
- [Implementing a store](./implementing-a-store.md#append-to-a-conversation): the store methods that keep conversations.
- [Builders](./builders.md#runoptionsbuilder): the run options of compaction and their defaults.
- [Models and providers](./models.md#the-summary-model): which model summarizes, and its provider.
- [Multiple agents](./multi-agent.md#handoff): handoffs, which need a conversation.
- [Serving a chat endpoint](./chat-endpoint.md): a conversation per chat, behind ASP.NET Core endpoints.
