# Conversations

A run with a conversation id resumes the conversation and stores everything it does in it, step by step.

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

The first run starts the conversation; the second resumes it: the stored messages go ahead of the new input, so the
model sees the whole conversation. The provider never receives the conversation id; Typeshift resends the
conversation itself. Settings ids and conversation ids are independent of each other; a run may pass either or both.

Typeshift resumes whichever conversation it is given. The host decides who may see a conversation: check the caller
against your own record of the conversation before you pass its id, as you would for any other id from a request.

## What is stored

A conversation is a list of entries, appended as they happen: a run's input, each model response with its model and
token usage, each tool result, and an entry that ends the run.

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var store = provider.GetRequiredService<ITypeshiftStore>();
var entries = await store.LoadConversationAsync(conversationId, ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L22-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A failed run ends with an `ErrorContent` entry, then the exception surfaces.
- A stream that fails, is cancelled or is left early ends with the updates that arrived.
- A cancelled run is stored too; storing does not observe the run's token.
- A store that fails to save is logged; the run still returns its answer.
- A run cut off by a crash has no ending entry; [resume it](#resuming).

On resume, tool calls that never got a result and error messages are left out, since providers reject them.

## Resuming

A run is stored as it goes: its input, every model response and every tool result, each the moment it exists. A host
that crashes mid-run loses at most the call in flight. `ResumeAsync` continues a conversation's interrupted run from
its last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.ResumeAsync(interrupted, settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L61-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A call interrupted while its tool ran is left out, so the model decides again; tools with side effects should be safe
to call twice. A new run on an interrupted conversation works too: it starts from the interrupted run's steps,
followed by its own input. Typeshift runs one run per conversation at a time in a process; across processes, the host
makes sure of it.

## Steering

A user can steer a run while it works: `TrySteer` hands input to the run of the conversation going on in this process.
The model receives it before its next call, the run does not end before it has, and it is stored with the
conversation like any input. With no run going on here, or one that is already ending, `TrySteer` returns false;
start a run with the input instead:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await support.RunAsync(input, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L48-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To stop a run instead, cancel its token; it is stored up to where it stopped.

## Live runs

`ConversationTracker` lists the runs of conversations going on in this process:

<!-- snippet: live-sessions -->
<a id='snippet-live-sessions'></a>
```cs
var tracker = provider.GetRequiredService<ConversationTracker>();

foreach (var run in tracker.Runs)
    Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L31-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-sessions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Live updates

With `WithRealtimeTracking()`, the updates of streaming runs are published as they happen. `WatchAsync` reads those
of one conversation, until cancelled; a UI forwards them over SignalR, say:

<!-- snippet: watch-conversation -->
<a id='snippet-watch-conversation'></a>
```cs
await foreach (var update in tracker.WatchAsync(conversationId, ct))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L73-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Text, reasoning and usage are merged and published at most every 100 ms, other updates at once. A watcher that
falls behind loses its oldest updates; a run never waits for a watcher. Both limits are options:

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

Live updates are in-process: a host with several processes watches on the process that runs the conversation.
