---
title: Serving a chat endpoint
description: Run an agent from ASP.NET Core endpoints, stream its answer to a chat UI as server-sent events, and answer the decisions a run waits for by their ids.
---

# Serving a chat endpoint

A chat UI talks to your agent through endpoints of your web app. The UI posts the user's message to a chat, gets the
answer back whole or as it streams, and posts the user's answer to each decision the run waits for, such as a tool
call that needs approval. This page builds those endpoints with ASP.NET Core minimal APIs, on the
[runs](./running-agents.md), [conversations](./conversations.md) and [decisions](./tool-approval.md) the other pages
describe.

## Post a message

Each chat is a conversation, and the chat's id in the route is its conversation id. The endpoint runs the agent on a
copy of the host with that conversation id and with the current customer's id as the
[settings id](./running-agents.md#stored-settings), so the customer's stored settings apply:

<!-- snippet: chat-endpoint -->
<a id='snippet-chat-endpoint'></a>
```cs
var chat = app.MapGroup("/chats/{chatId:int}")
    .AddEndpointFilter<ChatOwnerFilter>();

chat.MapPost("/messages", async (
    int chatId,
    ChatInput input,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    var response = await support
        .WithSettingsId(customer.Id)
        .WithConversationId(chatId)
        .RunAsync(input.Text, ct: ct);

    return ChatReply.From(response);
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L17-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The host and `ICurrentCustomer` come from the request's scope, as
  [Using an agent](./using-an-agent.md#inject-the-host-in-a-web-app) describes. `ICurrentCustomer` stands for a
  scoped service of your app, filled from the request's authentication, so put your authentication on the group too,
  for example with `RequireAuthorization()`.
- The `CancellationToken` is the request's, so the run is cancelled when the user leaves the page. A run that should
  finish anyway takes no token.
- `ChatReply` is the JSON the UI gets back: the answer's text and the decisions it waits for.

The filter on the group checks that the chat belongs to the caller before any endpoint of the group runs, because
Typeshift resumes whichever conversation it is given
([Conversation ids](./conversations.md#conversation-ids)). Map every endpoint that takes a chat id on
this group:

<!-- snippet: chat-owner-filter -->
<a id='snippet-chat-owner-filter'></a>
```cs
internal sealed class ChatOwnerFilter(IChats chats, ICurrentCustomer customer) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var chatId = int.Parse((string)http.Request.RouteValues["chatId"]!);

        return await chats.BelongsToAsync(chatId, customer.Id, http.RequestAborted)
            ? await next(context)
            : TypedResults.NotFound();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatOwnerFilter.cs#L6-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-owner-filter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`IChats` stands for your app's record of its chats, in which each chat names its customer. Another customer's chat
answers 404, as a chat that does not exist does, so the UI learns nothing about the chats of others.

## Return the answer

The endpoints answer with records of your own, so the UI's contract stays yours when Microsoft.Extensions.AI's types
change:

<!-- snippet: chat-contracts -->
<a id='snippet-chat-contracts'></a>
```cs
internal sealed record ChatInput(string Text);

// A decision writes its own JSON: its kind, its id, and what the UI shows to ask
internal sealed record ChatReply(string Text, IReadOnlyList<Decision> Decisions)
{
    public static ChatReply From(ChatResponse answer) => new(answer.Text, answer.Decisions);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatContracts.cs#L5-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-contracts' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Decisions` lists what the run waits for
([When a call needs approval](./tool-approval.md#when-a-call-needs-approval)). A decision writes its own JSON, so the
reply returns it as it is: its `kind`, `approval` or `plan`, its `id`, which the UI sends back with the user's
answer, and what the UI shows to ask, the `tool` with its `arguments` or the `plan`. Show the arguments as they are,
because those are the arguments the call runs with.

## Stream the answer

The streaming endpoint runs the agent the same way, and hands its updates to
[`TypedResults.ServerSentEvents`](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses):

<!-- snippet: chat-stream-endpoint -->
<a id='snippet-chat-stream-endpoint'></a>
```cs
chat.MapPost("/messages/stream", (
    int chatId,
    ChatInput input,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    var updates = support
        .WithSettingsId(customer.Id)
        .WithConversationId(chatId)
        .RunStreamingAsync(input.Text, ct: ct);

    return TypedResults.ServerSentEvents(Events(updates));
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L37-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each update becomes a `text` event with the text it adds, and each decision a `decision` event:

<!-- snippet: chat-stream-events -->
<a id='snippet-chat-stream-events'></a>
```cs
private static async IAsyncEnumerable<SseItem<object>> Events(IAsyncEnumerable<ChatResponseUpdate> updates)
{
    await foreach (var update in updates)
    {
        if (update.Text is { Length: > 0 } text) yield return new SseItem<object>(text, "text");

        // Decisions come with the run's last update, so each one the UI shows can be answered
        foreach (var decision in update.Decisions)
        {
            yield return new SseItem<object>(decision, "decision");
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L87-L101' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-events' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Both events carry JSON, written with your app's JSON options: a `text` event holds its text as a JSON string, and a
  `decision` event holds the decision as the reply does. Parse each event's data with `JSON.parse`.
- A browser reads the events with `fetch` and a reader over the response's body, because
  [`EventSource`](https://developer.mozilla.org/docs/Web/API/EventSource) sends only GET requests.
- A run that asks sends its `decision` events last, once it has ended, so the UI can post an answer the moment it
  shows the prompt. A reader that left the stream before then gets the decisions from the
  [endpoint below](#load-the-decisions).

## Follow a chat from elsewhere

The browser that posts a message reads the answer from its own stream. Another tab, or a support dashboard that
watches a customer's chat, follows it through `ConversationTracker<TId>.WatchAsync` instead
([Live updates](./conversations.md#live-updates)). This SignalR hub checks that the chat belongs to the caller, as the
group's filter does, and then streams the chat's text:

<!-- snippet: conversation-hub -->
<a id='snippet-conversation-hub'></a>
```cs
internal sealed class ConversationHub(ConversationTracker<int> tracker, IChats chats, ICurrentCustomer customer) : Hub
{
    public async IAsyncEnumerable<string> Watch(int chatId, [EnumeratorCancellation] CancellationToken ct)
    {
        if (!await chats.BelongsToAsync(chatId, customer.Id, ct)) yield break;

        await foreach (var update in tracker.WatchAsync(chatId, ct))
        {
            if (update.Text is { Length: > 0 } text) yield return text;
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ConversationHub.cs#L7-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation-hub' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The hub takes `ConversationTracker<int>` because the sample's chats have `int` ids. Inject the tracker of your
  chats' id type, such as `ConversationTracker<Guid>`; the container has one for every id type
  ([Live runs](./conversations.md#live-runs)).
- Register SignalR with `builder.Services.AddSignalR()`, and map the hub behind your authentication, for example
  `app.MapHub<ConversationHub>("/hub").RequireAuthorization()`. In a hub, the current customer comes from the
  connection's user, so `ICurrentCustomer` reads it from there.
- The browser calls `connection.stream("Watch", chatId)`. SignalR cancels `ct` when the browser disposes its
  subscription or the connection drops, which ends the loop.

## Load the decisions

After a reload the page has lost what the last answer asked. `GetDecisionsAsync` reads it again, from the chat's
decisions alone, without a run:

<!-- snippet: chat-decisions-endpoint -->
<a id='snippet-chat-decisions-endpoint'></a>
```cs
chat.MapGet("/decisions", async (int chatId, AgentHost<SupportAgent> support, CancellationToken ct) =>
    (await support.WithConversationId(chatId).GetDecisionsAsync(ct)).Open);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L54-L57' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-decisions-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The endpoint returns the open decisions, as the reply and the stream do. What it loaded also lists the tools
[approved for the conversation](./tool-approval.md#approving-for-the-conversation), in `Standing`.

## Answer a decision

The UI posts the user's answer to one decision, by the decision's id. `AnswerAsync` records it and, once no decision
of the chat is left open, carries the answers out and continues the run:

<!-- snippet: chat-answer-endpoint -->
<a id='snippet-chat-answer-endpoint'></a>
```cs
chat.MapPost("/decisions", async Task<IResult> (
    int chatId,
    DecisionAnswer answer,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    try
    {
        var resumed = await support
            .WithSettingsId(customer.Id)
            .WithConversationId(chatId)
            .AnswerAsync([answer], ct: ct);

        // Its Decisions list what still waits
        return TypedResults.Ok(ChatReply.From(resumed));
    }
    catch (DecisionClosedException)
    {
        // Answered already, in another tab or by a retry, or passed by a newer message: the UI drops it
        return TypedResults.Conflict();
    }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L59-L83' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-answer-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The body is a `DecisionAnswer`. `{"decisionId":"...","approved":true}` approves, `"approved":false` with a
  `"reason"` rejects and tells the model why, `"forConversation":true` makes the approval a
  [standing approval](./tool-approval.md#approving-for-the-conversation), and `"mode":"Auto"` picks the mode an
  approved [plan](./tool-approval.md#plan-mode) continues in.
- The UI sends back nothing of the call. Typeshift runs the call it stored under the id, with the arguments the user
  saw, and checks the answer against the stored decision
  ([Approving or rejecting](./tool-approval.md#approving-or-rejecting)).
- While other decisions of the chat are open, no model is called and the reply's `Decisions` lists them, so the UI
  posts one answer per prompt.
- An answer to a decision that has closed, for example one the user approved in another tab already, throws
  `DecisionClosedException` and records nothing. The endpoint answers it with 409 Conflict, so the UI can drop the
  prompt ([When a decision closes](./tool-approval.md#when-a-decision-closes)).
- Clear the UI's prompts when the user sends a new message instead, because the decisions that waited expire with it.
- The answering host has the customer's settings id again
  ([Tracking a conversation](./conversations.md#tracking-a-conversation)).

::: warning A chat runs one answer at a time
A conversation has one run at a time in a process ([One run at a time](./conversations.md#one-run-at-a-time)), so a
second run of a chat throws an `InvalidOperationException` before it stores anything. That happens to a message posted
while an answer still streams, or to a double click on send. The last answer to a decision posted while a run of the
chat goes on is recorded first and then throws the same way; post it again once that run has ended, and the chat
continues.
ASP.NET Core answers it with a 500 and logs an error. Disable the UI's send button while a run goes on, or pass a
message to the running answer with `TrySteer` ([Steering](./conversations.md#steering)). With several instances of
your app, nothing throws, so route each chat to one instance or keep its runs apart yourself.
:::

## Related

- [Using an agent](./using-an-agent.md): the host from a request's scope, a console app or a background job.
- [Streaming](./running-agents.md#streaming): what `RunStreamingAsync` returns.
- [Approval and modes](./tool-approval.md): decisions, their answers, rules and modes.
- [Conversations](./conversations.md): what a chat stores, resuming, steering and live updates.
