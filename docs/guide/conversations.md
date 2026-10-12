# Conversations

Every chat client `CreateChatClientAsync` returns tracks its exchanges with the model. One call, with all the tool
calls it triggers, is one session; sessions with the same conversation id form a conversation.

## Tracking a conversation

Pass a conversation id when you resolve the options:

<!-- snippet: conversation -->
<a id='snippet-conversation'></a>
```cs
var conversation = Guid.NewGuid();

var options = await support.ResolveChatOptionsAsync(customer, conversationId: conversation.ToString());
var client = await support.CreateChatClientAsync(options);

await client.GetResponseAsync("Where is my refund?", options);
await client.GetResponseAsync("When will it arrive?", options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L37-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The first call starts the conversation and stores it under the id when it ends. The second call resumes it: the
stored messages are sent ahead of the new input, so the model sees the whole conversation. The provider never
receives the id itself, since the tracker resends the conversation.

The id must parse as the store's id type. A call without an id, or with one that does not parse, is tracked while it
runs and not stored; so is every call when no store is registered.

## What is stored

A `StoredConversation` holds the name of the agent it ran under and every finished session: the input, every message
the model and the tools produced, the model, the token usage and why the model stopped.

<!-- snippet: stored-conversation -->
<a id='snippet-stored-conversation'></a>
```cs
var store = services.GetRequiredService<ITypeshiftStore<Guid>>();
var stored = await store.LoadAsync<StoredConversation>(conversation, CancellationToken.None);

Console.WriteLine($"{stored!.Sessions.Count} sessions, {stored.AggregatedUsage.TotalTokenCount} tokens");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L47-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-stored-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Sessions are stored whether they succeed or not:

- a failed call is stored with an `ErrorContent` message, then the exception surfaces;
- a stream that fails, is cancelled or is left early is stored with the updates that arrived;
- a cancelled call is stored as well; storing does not observe the call's token.

When a conversation is resumed, function calls that never got a result (a call cancelled mid-tool) are left out,
since providers reject them.

## Live sessions

`ConversationTracker` lists the sessions that are running and raises `OnConversationsChanged` when one starts or
ends:

<!-- snippet: live-sessions -->
<a id='snippet-live-sessions'></a>
```cs
var tracker = services.GetRequiredService<ConversationTracker>();

tracker.OnConversationsChanged += ct =>
{
    foreach (var session in tracker.Sessions)
        Console.WriteLine($"{session.ExecutionContext.Value} is running on {session.Model}");

    return Task.CompletedTask;
};
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L19-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-live-sessions' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Streaming sessions are listed in `RealtimeSessions`. Each publishes its updates through `OnSessionUpdated`: anything
but text, reasoning or usage at once, text at most every 100 ms; `OnSessionCompleted` fires when it ends. Every
handler gets a token that is cancelled at its time limit, and the call does not wait longer for it. An exception a
handler throws stays with the handler.
