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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
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
[`PersistAllConversations`](#keeping-every-conversation), a run stores nothing, so the next one starts from scratch. It
offers no handoffs, no tracker sees it, and it throws before it calls the model when its agent offers a tool whose
calls only your app could decide ([When a call needs approval](./tool-approval.md#when-a-call-needs-approval)).

### Keeping every conversation

To keep the runs of a host without a conversation id as well, turn on `PersistAllConversations`:

<!-- snippet: persist-all-conversations -->
<a id='snippet-persist-all-conversations'></a>
```cs
services.AddTypeshift()
    .WithOptions(options => options.PersistAllConversations = true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/KeepEveryConversation.cs#L16-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-persist-all-conversations' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Such a run then starts a conversation under a new id, and its response's `ConversationId` names it as text. The next
run continues it on a host with that id, read as the type your store keeps it in. Here `assistant` is the host of
[the smallest agent](./defining-agents.md#the-smallest-agent), which has no conversation id:

<!-- snippet: persist-all-conversations-run -->
<a id='snippet-persist-all-conversations-run'></a>
```cs
var first = await assistant.RunAsync("Where is my refund?");
var chat = assistant.WithConversationId(first.GetConversationId<Guid>());

var next = await chat.RunAsync("When will it arrive?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/KeepEveryConversation.cs#L27-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-persist-all-conversations-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`GetConversationId<Guid>()` parses the text back into the `Guid`, while `WithConversationId(first.ConversationId!)`
would name another conversation, under a `string` id. A handoff continues the conversation its run started.

Which runs start a conversation, and where it is kept:

| Run without a conversation id | Starts a conversation | Kept under |
|---|---|---|
| A run of the host, streaming or not | Yes | A new `Guid` of version 7, in your `IConversationStore<Guid>` |
| A call on the [raw path](./running-agents.md#the-raw-path), with the client's options, options of its own or none | Yes | The new `Guid`'s text, in your `IConversationStore<string>` |
| A raw-path call without input, which would continue an interrupted run ([Resuming](#resuming)) | No | Nothing is stored |
| A sub-agent's run | No | Its caller's conversation |

A raw-path call names its conversation in `ChatOptions.ConversationId`, a string, so the next call continues the
conversation with `options.ConversationId = response.ConversationId`.

New ids are `Guid`s of version 7, which sort by the time they were made. For a conversation store of another id type,
give the new ids with `NewConversationIds`:

<!-- snippet: new-conversation-ids -->
<a id='snippet-new-conversation-ids'></a>
```cs
services.AddTypeshift()
    .WithOptions(options =>
    {
        options.PersistAllConversations = true;
        options.NewConversationIds(() => $"chat-{Guid.CreateVersion7():N}");
    });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/KeepEveryConversation.cs#L45-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-new-conversation-ids' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The raw path takes these ids only when they are strings, and the `Guid`'s text otherwise.
- Without a store of the new ids' type, a run that starts a conversation throws an `InvalidOperationException` that
  names `NewConversationIds`.
- Runs that start together call the function at the same time, so make it thread-safe, and have it return an id no
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

Each entry is a `ConversationEntry`: the name of its agent in `Agent`, its `Messages`, the time it was stored in `At`
and its number in `Sequence`, which lets a store refuse a second run of the conversation
([One run at a time](#one-run-at-a-time)). A model response has its `Model` and `Usage` too, and the `Provider` its
agent ran on, as text, unless that was the default client. The entry that ends a run has `EndsRun`, and `IsSummary`
is true on the entry [compaction](#compaction) appends when it summarizes, which is how your code and your store tell
a summary apart ([Start from the last summary](./implementing-a-store.md#start-from-the-last-summary)). A model response of a run
with a maximum of input tokens also has `EstimatedInputTokens`, what compaction estimated its call at.

Any other entry may have a `Usage` too: the tokens the run spent beside its own model calls since its last entry, on
the runs of its [sub-agents](./multi-agent.md#sub-agents) and on the calls that write a [compaction](#compaction)
summary. The entry that stores a sub-agent's result, its background answer or the summary holds them, or the
run's next entry. So the entries' `Usage` adds up to everything the conversation's runs spent, as the response of
each run reports it, and the sum below counts every token your providers reported for the conversation.

Not every message in the user's role is the user's. A
[background sub-agent's answer](./multi-agent.md#in-the-background) joins a run as one, with the sub-agent's name as
its `AuthorName`, and so does the note that such a sub-agent failed or was lost. `message.IsBackgroundAnswer` is true
for those, so a chat view built from the entries leaves them out, or shows them as the sub-agent's
([What a chat page shows](#what-a-chat-page-shows)).

An entry serializes itself with `Serialize()` and `ConversationEntry.Deserialize(json)`, and JSON libraries handle it as
it is. `System.Text.Json` writes and reads that same JSON, and Newtonsoft.Json writes it as a string, so a store or an
API can pass entries on with either library and no settings.

The host of a conversation loads its entries, for a chat page that reloads or a job that looks for interrupted runs.
It reads through the store of the id's type, or the memory Typeshift keeps without one:

<!-- snippet: load-conversation -->
<a id='snippet-load-conversation'></a>
```cs
var entries = await support.WithConversationId(conversationId).LoadConversationAsync(ct);

Console.WriteLine($"{entries.Count} entries, {entries.Sum(entry => entry.Usage?.TotalTokenCount)} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L24-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-load-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Runs that fail or stop early are stored too:

- A failed run stores an entry with an `ErrorContent` that ends the run, then rethrows the exception to your code.
  The `ErrorContent` holds the exception's message as it is, which no model reads. Show your users a text of your
  own in its place, since a driver's or a provider's message may name what they should not see.
- A streaming run that fails, is cancelled, or that your code stops reading early stores the updates that had arrived.
  Its ending entry holds the exception's message, as a run's that does not stream does, and a stream your code left
  early, which no exception explains, ends with "The run stopped before it answered."
- A cancelled run is stored too, because Typeshift stores without the run's cancellation token. A run whose token
  was cancelled before it began throws an `OperationCanceledException` at once and stores nothing, whether your chat
  client honors the token or not.
- A run cut off by a crash has no ending entry, and [resuming](#resuming) continues it.

A store that fails to save a step fails the run ([When the store fails](#when-the-store-fails)).

Whenever a run loads the conversation, it builds what the model sees so that a provider accepts it. Each tool result
follows the message that made its call, even when the store holds input or an end of a run between the two. A call
that never got a result is left out, and so are a result whose call is missing and error messages, because providers
reject them. The approval requests and answers an earlier version of Typeshift stored among the entries are left out
too.

Some providers, such as local servers with an OpenAI-compatible API, number their tool calls per response, so a later
call can get an id the conversation holds already. Typeshift renames such a call, as `call_1_2`, before it stores or
runs it, so the stored entries, the decision that waits for the call and the tool's result all carry the new id.

### What a chat page shows

The entries hold what the model reads, which is more than the user of a chat reads. A chat page that loads them
shows:

- **The entries without `IsSummary`.** The entry that compaction appends repeats the messages it kept after its
  summary, which the entries before it hold already, and the summary itself is written for the model.
- **The text of the user's messages and of the agents' answers**, the messages of `ChatRole.User` and
  `ChatRole.Assistant`. Tool calls, their results and the `ErrorContent` that ends a failed run hold no text.
- **No background answer.** A [background sub-agent's answer](./multi-agent.md#in-the-background) is a message of the
  user's role that the user never wrote, for which `message.IsBackgroundAnswer` is true. The calling agent's answer
  after it says what the user needs of it.

[Load the chat](./chat-endpoint.md#load-the-chat) shows an endpoint that returns these lines. An answer the model is
still writing is in no entry until its model call ends, so the page follows it through the
[live updates](#live-updates).

### When the store fails

When the store fails to save a step, the run goes on to its end, storing what it can, and then throws a
`ConversationStoreException`. Its `Response` holds what the run answered, and its inner exception is the store's.
The step it failed to save is missing from later runs, so a run that answered is no success: a lost end makes a
finished run look interrupted, and a lost answer is gone at the next page load. Each failure is also
[logged](./observability.md#logs) and recorded on the run's span, which is an error.

- An app that would rather show the answer catches the exception and returns its `Response`, which lists the run's
  decisions too when it asked any.
- An app can choose that such runs return instead:
  `AddTypeshift().WithOptions(options => options.StoreFailure = StoreFailure.ContinueRun)`. The response then has
  `ConversationIncomplete` set, and so does the last update of a stream, so your app can tell the user that the
  chat may not have kept that answer. The span is an error all the same.
- A run that asks your app throws the store's exception at once when it could not store the model's response or the
  decision, whatever the option says, because a prompt your app shows must be one it can answer.

## Resuming

Each model response is stored as it arrives, and tool results before the next model call, so an app that crashes
mid-run loses at most the calls in flight. `ResumeAsync` continues the interrupted run of the host's conversation from
its last stored step, without new input:

<!-- snippet: resume -->
<a id='snippet-resume'></a>
```cs
var resumed = await support.WithSettingsId(settingsId).WithConversationId(interrupted).ResumeAsync();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L74-L76' title='Snippet source file'>snippet source</a> | <a href='#snippet-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ResumeStreamingAsync` does the same and streams the continuation, as `RunStreamingAsync` streams a run.

`ResumeAsync` also continues a conversation whose decisions your app answered with `RecordAnswersAsync`: it carries
the answers out and goes on. While decisions are still open, it calls no model and returns them
([Approving or rejecting](./tool-approval.md#approving-or-rejecting)). After your app withdrew them with
`CancelDecisionsAsync`, the model reads that the request was withdrawn and goes on
([When a decision closes](./tool-approval.md#when-a-decision-closes)).

`ResumeAsync` throws an `InvalidOperationException` on a host without a conversation id, and a
`NothingToResumeException`, an `InvalidOperationException` too, when the conversation's last run ended and no
answered or withdrawn decision waits. A new run with input works on an interrupted conversation too: it starts
from the interrupted run's steps and adds its own input.

A conversation whose last entry is the model's answer has nothing left to do, even though no entry ends its run:
the end was lost between the two appends, in a crash or a failed save. `ResumeAsync` then stores the end and returns
that answer, and calls no model.

After a restart, the entries tell you which conversations to resume. A run stores its ending entry only when it ends,
so the last entry that `LoadConversationAsync` returns has `EndsRun` false for a run that a crash cut off and for one
that is still going on.

With one instance of your app, nothing else can be running the conversation after a restart, so such an entry means
the run was interrupted. With several instances, or during a rolling deploy, another instance may be answering that
chat at this moment. A store that refuses a second run's entries stops whichever of the two runs appends second
([Running on several instances](#running-on-several-instances)), but a resume calls the model before it stores
anything, so that call is paid for, and the run it stops may be the one that was answering. There, resume only when
the last entry's `At` is older than a limit your app chooses, or behind a lease of your own.

::: warning A tool can be called twice
A call interrupted while its tool ran is left out on resume. The model decides again and may call the tool a second
time, so make tools with side effects safe to repeat. A call your app approved is the exception: no later run starts
it again, and the model reads that it is not known whether the call took effect.
:::

## Retrying a failed run

A run that fails at the model call, on a provider's error, a rate limit or a timeout, has stored the user's message
already, and its end holds the error ([What is stored](#what-is-stored)). Running the agent again with the same input
would put the message into the conversation twice, and `ResumeAsync` refuses a conversation whose last run ended.
`RetryAsync` continues it from that run's stored steps instead, so the model receives the message once:

<!-- snippet: retry -->
<a id='snippet-retry'></a>
```cs
try
{
    await refunds.RunAsync(message);
}
catch (HttpRequestException)
{
    // The conversation holds the message already, and the retry sends it once more
    await refunds.RetryAsync();
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L83-L93' title='Snippet source file'>snippet source</a> | <a href='#snippet-retry' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `RetryAsync` continues what `ResumeAsync` continues too: an interrupted run, or decisions your app answered. On top,
  it continues a run that failed, was cancelled, or whose stream its reader left.
- It throws a `NothingToResumeException` when the conversation's last run answered or waits for your app, which a
  "Retry" button that is pressed twice meets.
- `RetryStreamingAsync`, `RetryAsync<TResult>` and `RetryStreamingAsync<TResult>` stream, or ask for `TResult` again,
  as the resumes do. A tool call the failed run left running is left out, as on [resume](#resuming).

## One run at a time

Within one container, your app's or that of a runtime from `TypeshiftRuntime.Create`, a conversation has one run at
a time. Starting a second while the first goes on throws a `ConversationBusyException`, before the second run stores
anything, so [steer](#steering) the running one or start again once it has ended. Its `Conversation` names the
conversation, and it is an `InvalidOperationException`, so a `catch` of that type still catches it.

Two moments of a run take no steering input and throw no `ConversationBusyException` either: a run that starts
while another is still loading the conversation waits until that run has begun, and a run that starts while
another is ending, storing its end, waits until it has ended.

Across containers, such as two instances of your app behind a load balancer, your store keeps a conversation's runs
apart ([Running on several instances](#running-on-several-instances)).

## Live runs

`ConversationTracker<TId>` tells your app which conversations under ids of `TId` have a run going on in this
container.
It also hands out each run's updates, so a second tab or a support dashboard can follow a chat that another request
is answering ([Live updates](#live-updates)).

Inject the tracker of your conversation ids' type, or resolve it: the container has one for every id type, a
singleton, so the docs samples, whose conversations are `int`s, take `ConversationTracker<int>`. Its `Runs` lists the
runs going on in this container under ids of that type, each with its conversation, the agent that answers, its model
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L117-L120' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-runs' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: warning The tracker lists every customer's runs
`Runs` and `WatchRunsAsync` hold every run under ids of the tracker's type, whoever started it. Keep only the
conversations the caller may see before you return them from an endpoint.
:::

::: tip Native AOT needs the tracker of a value type registered
An app published with Native AOT that injects `ConversationTracker<Guid>`, or the tracker of another value type,
registers it with `WithConversationTracker<Guid>()`
([Trackers of value-type ids](./native-aot.md#trackers-of-value-type-ids)).
:::

## Steering

`TrySteer` hands new input to a conversation's run while it works, for example when a user adds a request halfway
through. The model receives it before its next call, and the run does not end before then. The input is stored like
any other. Your run middleware sees it first and may drop or replace it
([Steering input](./middleware.md#steering-input)). When the run had asked your app before it took the input, its
decisions expire, as they would for a new message: the model reads that those calls did not run, and the run's
response lists no decision ([When a decision closes](./tool-approval.md#when-a-decision-closes)). A run that fails,
is cancelled or has spent its limit of model calls before it answers the input stores the input with its end, and the
conversation's next run reads it.

`TrySteer` takes text, or a user's `ChatMessage` that carries images or files, as a run's input does. A message of
another role, or one with Microsoft.Extensions.AI's approval contents, throws an `ArgumentException`.

`TrySteer` returns false when no run of the conversation takes input in this container: none is going on, one is
still loading the conversation, or it is ending. Start a run with the input instead. A run that started in the
meantime throws a `ConversationBusyException`, and then takes the input when you steer again:

<!-- snippet: steering -->
<a id='snippet-steering'></a>
```cs
var input = "Send me the invoice too.";

while (!tracker.TrySteer(conversationId, input))
{
    try
    {
        await chat.WithSettingsId(settingsId).RunAsync(input);
        break;
    }
    catch (ConversationBusyException)
    {
        // A run of the conversation began in between and takes the input now
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L51-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run holds at most 32 inputs, or 262,144 characters of their text, that its model has not received yet. Input
beyond that makes `TrySteer` throw a `ConversationBusyException`, which leaves the loop above, so an endpoint
answers it as it answers any chat that is busy
([A chat that is answering](./chat-endpoint.md#a-chat-that-is-answering)). The model takes what waits with its next
call, and the run takes input again from then on. Without the bound, a client that posts input faster than the
model answers would grow that call, and the stored conversation, without end. A message your app steers must not
carry the mark of a [background answer](./multi-agent.md#in-the-background), which only a run gives: `TrySteer`
throws an `ArgumentException` for one, since such an answer passes your run middleware unseen.

To stop a run, cancel its token. It is stored up to where it stopped.

## Live updates

Typeshift publishes the updates of runs as they happen, and `WatchAsync` reads one conversation's updates, across
its runs, until you cancel its token, which ends the loop with an `OperationCanceledException`, as with
`WatchRunsAsync`. A streaming run publishes its updates as they stream; a run that does not stream publishes each
model response and tool result as it stores it. Each run ends with an update whose `EndsRun` is true, so a watcher
that waits for one answer stops there. A browser can follow the updates over SignalR, as
[Follow a chat from elsewhere](./chat-endpoint.md#follow-a-chat-from-elsewhere) shows:

<!-- snippet: watch-conversation -->
<a id='snippet-watch-conversation'></a>
```cs
await foreach (var update in tracker.WatchAsync(conversationId, ct))
{
    Console.Write(update.Text);

    if (update.EndsRun) break;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L103-L110' title='Snippet source file'>snippet source</a> | <a href='#snippet-watch-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A watcher receives what the runs write from the moment it subscribes: the model's updates, the tool calls and their
results, the decisions a run asks, and the run's end:

- **What the store does not hold yet.** A watcher that joins while a run goes on first receives what the run
  published since its last stored step, such as the answer the model is writing, merged into one update per
  message. The store holds that answer only once its model call ends, so a page that opens a chat loads what is
  stored, then watches, and shows the answer from its start.
- **The end.** A run's last update has `EndsRun` set, after its other updates. A run that answered carries the
  `FinishReason` of its last model call, and one that failed or stopped carries the error its stored end holds, as
  an `ErrorContent`. A handoff ends once, after the answer of the agent handed to.

That leaves out two things:

- **Input.** The user's message, steering input and a background sub-agent's answer go to the model and the store
  but not to the watchers.
- **Other containers.** A watcher sees only the runs of its own container. With several instances of your app, watch
  in the one that runs the conversation ([Running on several instances](#running-on-several-instances)).

A step the run stores between the page's load and its watch is in neither, so a page that shows a whole chat loads
it again when it sees `EndsRun`.

Text, reasoning and usage updates are merged and published at most every 100 ms, and other updates go out at once.
Merged text never spans two messages: text of another message goes out as an update of its own, and the last of an
answer goes out as its model call ends, so a watcher has the whole answer while the run waits for steering input or
a background agent. A watcher sees a handoff's call, its result, then the answer of the agent handed to. A run
never waits for a watcher, so a watcher that falls more than 256 updates behind loses the oldest ones, and
Typeshift [logs a warning](./observability.md#logs) the first time it does. You can change both limits in
`TypeshiftOptions.LiveUpdates`, through `WithOptions`:

<!-- snippet: live-update-options -->
<a id='snippet-live-update-options'></a>
```cs
services.AddTypeshift()
    .WithOptions(options =>
    {
        options.LiveUpdates.PublishInterval = TimeSpan.FromMilliseconds(250);
        options.LiveUpdates.MaxPendingUpdates = 1024;
    });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L67-L74' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-update-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Running on several instances

Behind a load balancer, or during a rolling deploy, two instances of your app can serve the same chat. Each instance
has a container of its own, so what Typeshift keeps in memory stays in that instance, and only what it keeps in your
store reaches the other. Through your store, these hold across instances:

- **One run at a time**, when your store refuses an entry under a number its conversation holds already
  ([Refuse a second run](./implementing-a-store.md#refuse-a-second-run)), as the store in memory and the EF Core store
  of these docs do. Of two runs that loaded the conversation at the same point, the one that appends second stops
  with a `ConversationBusyException` before its next model call or tool call. What it stored before stays, and its
  `InnerException` is the store's `StoreConflictException`.
- **Decisions.** Each change of a conversation's decisions is saved against the version it loaded
  ([Keep decisions](./implementing-a-store.md#keep-decisions)), so a decision is answered once and the calls it
  approves start once, whichever instance answers or carries them out, as with a second tab
  ([When a decision closes](./tool-approval.md#when-a-decision-closes)).
- **The conversation's mode and its standing approvals**, which are stored with its decisions.

A run the store refused keeps what it did before: a tool it called has run, and its result is not stored, so the
model of the other run never reads it. The other run's answer is the conversation's, and a run whose end alone the
store refused answered already and returns its answer. With a store that does not refuse a taken number, the entries
of the two runs interleave.

These stay in the container that runs the conversation:

- The `ConversationBusyException` a second run throws before it stores anything. Another instance starts its run at
  once, and the store stops one of the two only once it appends, after that run may have called the model.
- Steering: `TrySteer` returns false in an instance where no run of the conversation goes on.
- The tracker's `Runs`, `WatchRunsAsync` and live updates.
- Knowing whether a run goes on. Nothing leases a conversation, so after a restart an interrupted run and one that
  another instance is answering look the same.

So route each conversation to one instance, for example with sticky sessions keyed by the conversation id. A second
message then throws at once, and steering input and watchers reach the run. Resume a conversation only when no other
instance can be running it ([Resuming](#resuming)).

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L23-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
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
   call fails, the run goes on: its own model writes the summary, or the call goes out without one.

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

- [Running an agent](./running-agents.md): what a run takes and returns.
- [How a run works](./how-a-run-works.md): where a run resumes and stores its steps.
- [Implementing a store](./implementing-a-store.md#append-to-a-conversation): the store methods that keep conversations.
- [Builders](./builders.md#runoptionsbuilder): the run options of compaction and their defaults.
- [Models and providers](./models.md#the-summary-model): which model summarizes, and its provider.
- [Sub-agents and handoffs](./multi-agent.md#handoff): handoffs, which need a conversation.
- [Serving a chat endpoint](./chat-endpoint.md): a conversation per chat, behind ASP.NET Core endpoints.
