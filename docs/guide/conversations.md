---
title: Conversations
description: Store a conversation under an id, resume it after a crash, steer and watch a run, and compact a long one.
---

# Conversations

Pass a conversation id to a run, and Typeshift stores every step under it. The next run picks up where the last one
ended, even after a crash.

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

The first run starts the conversation and the second resumes it. Typeshift puts the stored messages ahead of the new
input, so the model sees the whole conversation. Because Typeshift sends the history itself, it never passes the
conversation id on to the provider. Settings ids and conversation ids are independent, and a run may pass either or
both. A conversation does not remember a settings id, so pass it to every run of the conversation that should use the
stored settings, the run that answers an approval included.

Typeshift never creates ids ([the id type](./registration.md#the-id-type)). You choose the conversation id, for
example the key of your own record of the conversation, and pass the same id to every run of that conversation.

::: danger Check the caller before you pass a conversation id
Typeshift resumes whichever conversation it is given, so deciding who may see one is your app's job. Check the caller
against your own record of the conversation, as you would for any other id from a request.
:::

## What is stored

A conversation is a list of entries, appended as they happen: each run's input, every model response with its model and
token usage, the tool results of each round of calls, and an entry that ends the run and records its mode. Your store
receives each entry through `AppendConversationAsync` ([Implementing a store](./implementing-a-store.md#append-to-a-conversation)).

Each entry is a `ConversationEntry`: the name of its agent in `Agent`, its `Messages` and the time it was stored in
`At`, plus the `Model` and `Usage` of a model response, and `EndsRun` on the entry that ends a run. `IsSummary` is true
on the entry [compaction](#compaction) appends when it summarizes, which is how your code and your store tell a summary
apart ([Start from the last summary](./implementing-a-store.md#start-from-the-last-summary)).

An entry serializes itself with `Serialize()` and `ConversationEntry.Deserialize(json)`, and JSON libraries handle it as
it is. `System.Text.Json` writes and reads that same JSON, and Newtonsoft.Json writes it as a string, so a store or an
API can pass entries on with either library and no settings.

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var store = provider.GetRequiredService<IConversationStore>();
var entries = await store.LoadConversationAsync(conversationId, ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L22-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Runs that fail or stop early are stored too:

- A failed run stores an entry with an `ErrorContent` that ends the run, then rethrows the exception to your code.
- A streaming run that fails, is cancelled, or that your code stops reading early stores the updates that had arrived.
- A cancelled run is stored too, because Typeshift stores without the run's cancellation token.
- A run cut off by a crash has no ending entry, and [resuming](#resuming) continues it.

When the store fails to save, the failure is logged and recorded on the run's span, and the run still returns its
answer. Whenever a run loads the conversation, it leaves tool calls that never got a result and error messages out of
what the model sees, because providers reject them.

## Resuming

Each model response is stored as it arrives, and tool results before the next model call, so an app that crashes
mid-run loses at most the calls in flight. `ResumeAsync` continues the conversation's interrupted run from its last
stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.ResumeAsync(interrupted, settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L63-L65' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

On a conversation whose last run ended, `ResumeAsync` throws an `InvalidOperationException`. After a restart, the
entries tell you which conversations to resume: when the last entry that `LoadConversationAsync` returns has
`EndsRun` false, a crash cut that run off. A new run with input works on an interrupted conversation too: it starts
from the interrupted run's steps and adds its own input.

::: warning A tool can be called twice
A call interrupted while its tool ran is left out on resume. The model decides again and may call the tool a second
time, so make tools with side effects safe to repeat.
:::

## One run at a time

Within a process, a conversation has one run at a time. Starting a second while the first goes on throws an
`InvalidOperationException`, so [steer](#steering) the running one or wait until it ends. A run that starts while
another is still loading the conversation waits until that run has begun. Across processes, keeping to one run per
conversation is up to your app.

## Live runs

`ConversationTracker` lists the runs with a conversation id that are going on in this process, each with its
conversation, the agent that answers, its model and when it started:

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
runs going on, then each run as it starts and again as it ends, with `Ended` set, until you cancel its token.
Cancelling ends the loop with an `OperationCanceledException`, so catch it where you stop watching. It works without
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

`TrySteer` hands new input to a conversation's run while it works, for example when a user adds a request halfway
through. The model receives it before its next call, and the run does not end before then. The input is stored like
any other.

`TrySteer` returns false when no run of the conversation is going on in this process, or when that run is already
ending. Start a run with the input instead:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await support.RunAsync(input, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L50-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To stop a run, cancel its token. It is stored up to where it stopped.

## Live updates

With `WithRealtimeTracking()`, Typeshift publishes the updates of runs as they happen, and `WatchAsync` reads one
conversation's updates until you cancel its token, which ends the loop with an `OperationCanceledException`, as with
`WatchRunsAsync`. A streaming run publishes its updates as they stream; a run that does not stream publishes each
model response and tool result as it stores it. A UI can forward them over SignalR, for example:

<!-- snippet: watch-conversation -->
<a id='snippet-watch-conversation'></a>
```cs
await foreach (var update in tracker.WatchAsync(conversationId, ct))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L75-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Text, reasoning and usage updates are merged and published at most every 100 ms, and other updates go out at once. A
run never waits for a watcher, so a watcher that falls more than 256 updates behind loses the oldest ones. You can
change both limits with `RealtimeTrackingOptions`:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L51-L58' title='Snippet source file'>snippet source</a> | <a href='#snippet-realtime-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Without `WithRealtimeTracking()`, `WatchAsync` throws an `InvalidOperationException`.

::: warning Live updates stay in their process
A watcher sees only the runs of its own process. When your app runs in several processes, watch in the process that
runs the conversation.
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
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations)
    .Configurable(r => r.SummaryModel);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L20-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Before every model call, Typeshift estimates the call's input tokens at about four characters per token, corrected by
what the provider reported for the run's last call, or for the conversation's last call before the run. When the
estimate reaches the maximum, it shrinks the history to `WithTargetInputTokens`, half the maximum unless set:

1. It clears the results of the oldest tool calls the model has already read. It keeps the calls, so the model knows
   what it did and can call again.
2. If that is not enough, it summarizes the oldest turns with the [summary model](./models.md#the-summary-model), the
   run's own unless you name another.

The instructions and the latest call of a tool named `write_todos`, if the agent offers one, always stay, and a cut
never separates a tool call from its result.
Every later model call of the same run starts from the history as compaction left it, with the same results cleared
and the same summary.

The stored conversation keeps everything. Compaction appends an entry of its own, with `IsSummary` set to true, that
holds the summary and the messages it kept after it. Later runs start from that entry instead of summarizing again, and
read nothing before it: the entry also keeps the conversation's mode, its [approvals for the
conversation](./tool-approval.md#approving-for-the-conversation) and the background agents still working.

## Related

- [Running agents](./running-agents.md): passing a conversation id to a run.
- [How a run works](./running-agents.md#how-a-run-works): where a run resumes and stores its steps.
- [Implementing a store](./implementing-a-store.md#append-to-a-conversation): the store methods that keep conversations.
- [Builders](./builders.md#runoptionsbuilder): the run options of compaction and their defaults.
- [Models and providers](./models.md#the-summary-model): which model summarizes, and its provider.
- [Multiple agents](./multi-agent.md#handoff): handoffs, which need a conversation.
- [Serving a chat endpoint](./chat-endpoint.md): a conversation per chat, behind ASP.NET Core endpoints.
