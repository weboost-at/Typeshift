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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Conversations.cs#L13-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first run starts the conversation; the second resumes it: the stored messages go ahead of the new input, so the
model sees the whole conversation. The provider never receives the conversation id; Typeshift resends the
conversation itself.

Typeshift resumes whichever conversation it is given. The host decides who may see a conversation: check the caller
against your own record of the conversation before you pass its id, as you would for any other id from a request.

## What is stored

A conversation is a list of entries, appended as they happen: a run's input, each model response with its model and
token usage, each tool result, and compaction summaries. The last entry of a run says it ended.

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var store = provider.GetRequiredService<ITypeshiftStore>();
var entries = await store.LoadConversationAsync(conversationId, ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Conversations.cs#L20-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A failed run ends with an `ErrorContent` entry, then the exception surfaces.
- A stream that fails, is cancelled or is left early ends with the updates that arrived.
- A cancelled run is stored too; storing does not observe the run's token.
- A store that fails to save is logged and traced; the run still returns its answer.
- A run cut off by a crash has no ending entry; [resume it](./long-running-agents.md#resuming).

On resume, tool calls that never got a result and error messages are left out, since providers reject them.
[Compaction](./long-running-agents.md#compaction) shortens long conversations.

## Live runs

`ConversationTracker` lists the runs of conversations going on in this process:

<!-- snippet: live-sessions -->
<a id='snippet-live-sessions'></a>
```cs
var tracker = provider.GetRequiredService<ConversationTracker>();

foreach (var run in tracker.Runs)
    Console.WriteLine($"{run.Agent} is answering conversation {run.Conversation} on {run.Model}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Conversations.cs#L27-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-sessions' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Conversations.cs#L34-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Registration.cs#L32-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-realtime-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Live updates are in-process: a host with several processes watches on the process that runs the conversation.
