---
title: Conversations
description: Store a conversation under an id, resume it after a crash, steer a run while it works and watch it live.
---

# Conversations

A run with a conversation id resumes that conversation and stores everything it does in it, step by step. The next run
with the same id picks up where the last one ended, and a run that a crash cut off continues from its last stored step.
While a run works, a user can steer it with new input, and a UI can watch its updates.

::: tip The short version

- Pass a `conversationId` to a run. It resumes the conversation and appends every step to your store.
- Typeshift resends the stored conversation to the model on every run. The provider never sees the id.
- `ResumeAsync` continues a run a crash cut off, from its last stored step.
- `ConversationTracker` lists the runs going on in this process, reports each start and end to `WatchRunsAsync`,
  steers them with `TrySteer` and, with `WithRealtimeTracking()`, streams their updates to watchers.
- Typeshift resumes any conversation it is given. You decide who may see one.

:::

## Tracking a conversation

<!-- snippet: conversation -->
<a id='snippet-conversation'></a>
```cs
var conversationId = 7;

await support.RunAsync("Where is my refund?", conversationId: conversationId);
await support.RunAsync("When will it arrive?", conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L15-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first run starts the conversation, and the second resumes it. The stored messages go ahead of the new input, so the
model sees the whole conversation. The provider never receives the conversation id: Typeshift resends the conversation
itself. Settings ids and conversation ids are independent of each other, and a run may pass either or both.

::: danger Check the caller before you pass a conversation id
Typeshift resumes whichever conversation it is given. The host decides who may see a conversation. Check the caller
against your own record of the conversation before you pass its id, as you would for any other id from a request.
:::

## What is stored

A conversation is a list of entries, appended as they happen: a run's input, each model response with its model and
token usage, each tool result, and an entry that ends the run and holds the mode it ended in. Your store receives each
entry through `AppendConversationAsync`; see [the store](./settings-and-stores.md#the-store).

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var store = provider.GetRequiredService<ITypeshiftStore>();
var entries = await store.LoadConversationAsync(conversationId, ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L22-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run that fails or stops early is stored too:

- A failed run ends with an `ErrorContent` entry, then the exception surfaces.
- A stream that fails, is cancelled or is left early ends with the updates that arrived.
- A cancelled run is stored, since storing does not observe the run's token.
- A run cut off by a crash has no ending entry. [Resume it](#resuming).

A store that fails to save is logged and recorded on the run's span. The run still returns its answer.

On resume, a run leaves out the tool calls that never got a result and the error messages, since providers reject them.

## Resuming

A run is stored as it goes: its input, every model response and every tool result, each the moment it exists. A host
that crashes mid-run loses at most the call in flight. `ResumeAsync` continues a conversation's interrupted run from its
last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.ResumeAsync(interrupted, settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L63-L65' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

On a conversation whose last run ended, `ResumeAsync` throws an `InvalidOperationException`. A new run on an interrupted
conversation works too: it starts from the interrupted run's steps, followed by its own input.

::: warning A tool can be called twice
A call interrupted while its tool ran is left out on resume, so the model decides again and may call the tool a second
time. Make tools with side effects safe to call twice.
:::

## One run at a time

Typeshift runs one run per conversation at a time in a process. A second run of a conversation that is going on in this
process throws an `InvalidOperationException`. [Steer](#steering) the running one instead, or wait until it ends. Across
processes, the host makes sure that one run of a conversation goes on at a time.

## Live runs

`ConversationTracker` lists the runs of conversations going on in this process. Each names the conversation, the agent
that answers, its model and when it started:

<!-- snippet: live-sessions -->
<a id='snippet-live-sessions'></a>
```cs
var tracker = provider.GetRequiredService<ConversationTracker>();

foreach (var run in tracker.Runs)
    Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L31-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-sessions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`WatchRunsAsync` tells a UI when that list changes, so a list of running conversations needs no polling. It yields the
runs going on, then each run as it starts and as it ends, with `Ended` set, until you cancel it. It needs no
`WithRealtimeTracking()`:

<!-- snippet: watch-runs -->
<a id='snippet-watch-runs'></a>
```cs
await foreach (var run in tracker.WatchRunsAsync(ct))
    Console.WriteLine($"Conversation {run.Conversation} is {(run.Ended ? "idle" : "running")}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L90-L93' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-runs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Steering

A user can steer a run while it works. `TrySteer` hands input to the run of the conversation going on in this process.
The model receives it before its next call, and the run does not end before it has. It is stored with the conversation
like any input.

`TrySteer` returns false when no run of the conversation is going on here, or when that run is already ending. Start a
run with the input instead:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await support.RunAsync(input, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L50-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To stop a run instead, cancel its token. It is stored up to where it stopped.

## Live updates

With `WithRealtimeTracking()`, Typeshift publishes the updates of streaming runs as they happen. `WatchAsync` reads the
updates of one conversation until you cancel it. A UI can forward them over SignalR, for example:

<!-- snippet: watch-conversation -->
<a id='snippet-watch-conversation'></a>
```cs
await foreach (var update in tracker.WatchAsync(conversationId, ct))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L75-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Text, reasoning and usage updates are merged and published at most every 100 ms. Other updates go out at once. A watcher
may fall behind by 256 updates. Beyond that it loses its oldest ones, since a run never waits for a watcher. Both limits
are options:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L49-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-realtime-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Without `WithRealtimeTracking()`, `WatchAsync` throws an `InvalidOperationException`.

::: warning Live updates stay in their process
A watcher sees only the runs of its own process. A host with several processes watches on the process that runs the
conversation.
:::

## Related

- [Running agents](./running-agents.md): passing a conversation id to a run.
- [How a run works](./how-a-run-works.md): where a run resumes and stores its steps.
- [Settings and stores](./settings-and-stores.md#the-store): the store methods that keep conversations.
- [Long-running agents](./long-running-agents.md): runs of hundreds of model calls, and compaction.
- [Multiple agents](./multi-agent.md#handoff): handoffs, which need a conversation.
