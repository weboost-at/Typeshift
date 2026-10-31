---
title: Serving a chat endpoint
description: Run an agent from ASP.NET Core endpoints, stream its answer to a chat UI as server-sent events, and answer tool approvals by their ids.
---

# Serving a chat endpoint

A chat UI talks to your agent through endpoints of your web app. The UI posts the user's message to a chat, gets the
answer back whole or as it streams, and posts the user's answers to the tool calls that wait for approval. This page
builds those endpoints with ASP.NET Core minimal APIs, on the [runs](./running-agents.md),
[conversations](./conversations.md) and [approvals](./tool-approval.md) the other pages describe.

## Post a message

Each chat is a conversation, and the chat's id in the route is its conversation id. The endpoint runs the agent in
that conversation, with the current customer's id as the [settings id](./running-agents.md#stored-settings), so the
customer's stored settings apply:

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
    var response = await support.RunAsync(
        input.Text,
        settingsId: customer.Id,
        conversationId: chatId,
        ct: ct);

    return ChatReply.From(response);
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L17-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The host and `ICurrentCustomer` come from the request's scope, as
  [Using an agent](./using-an-agent.md#inject-the-host-in-a-web-app) describes. `ICurrentCustomer` stands for a
  scoped service of your app, filled from the request's authentication, so put your authentication on the group too,
  for example with `RequireAuthorization()`.
- The `CancellationToken` is the request's, so the run is cancelled when the user leaves the page. A run that should
  finish anyway takes no token.
- `ChatReply` is the JSON the UI gets back: the answer's text and the approvals it waits for.

The filter on the group checks that the chat belongs to the caller before any endpoint of the group runs, because
Typeshift resumes whichever conversation it is given
([Tracking a conversation](./conversations.md#tracking-a-conversation)). Map every endpoint that takes a chat id on
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

internal sealed record ChatReply(string Text, IReadOnlyList<PendingApproval> Approvals)
{
    public static ChatReply From(ChatResponse answer) =>
        new(answer.Text, [.. answer.ApprovalRequests.Select(PendingApproval.From).OfType<PendingApproval>()]);
}

// What the UI shows to ask, and sends back with the user's answer
internal sealed record PendingApproval(
    string RequestId,
    string CallId,
    string Tool,
    IDictionary<string, object?>? Arguments)
{
    public static PendingApproval? From(ToolApprovalRequestContent request) =>
        request.ToolCall is FunctionCallContent call
            ? new(request.RequestId, call.CallId, call.Name, call.Arguments)
            : null;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatContracts.cs#L5-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-contracts' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ApprovalRequests` lists the calls that wait for approval ([A run that asks](./tool-approval.md#a-run-that-asks)).
`PendingApproval` keeps the tool and its arguments, which the UI shows when it asks, and the ids the UI sends back with
the user's answer. Show the arguments as the model sent them, because those are the arguments the call runs with.

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
    var updates = support.RunStreamingAsync(
        input.Text,
        settingsId: customer.Id,
        conversationId: chatId,
        ct: ct);

    return TypedResults.ServerSentEvents(Events(updates));
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L38-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each update becomes a `text` event with the text it adds, and each approval request an `approval` event:

<!-- snippet: chat-stream-events -->
<a id='snippet-chat-stream-events'></a>
```cs
private static async IAsyncEnumerable<SseItem<object>> Events(IAsyncEnumerable<ChatResponseUpdate> updates)
{
    await foreach (var update in updates)
    {
        if (update.Text is { Length: > 0 } text) yield return new SseItem<object>(text, "text");

        foreach (var request in update.Contents.OfType<ToolApprovalRequestContent>())
        {
            if (PendingApproval.From(request) is { } approval)
                yield return new SseItem<object>(approval, "approval");
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L89-L103' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-events' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Both events carry JSON, written with your app's JSON options: a `text` event holds its text as a JSON string, and an
  `approval` event holds the `PendingApproval` with camelCase names. Parse each event's data with `JSON.parse`.
- A browser reads the events with `fetch` and a reader over the response's body, because
  [`EventSource`](https://developer.mozilla.org/docs/Web/API/EventSource) sends only GET requests.
- A run that asks for approval stores the requests, sends its `approval` events last, and then ends. Post the answers
  once the stream has ended, when the reader reports that it is done. A reader that stops at an `approval` event
  leaves the run to end on its own, and the answers still work.

## Answer the approvals

The UI posts the user's answer to each pending approval. The endpoint turns the answers into
`ToolApprovalResponseContent`s and runs the conversation's next run with them:

<!-- snippet: chat-approval-endpoint -->
<a id='snippet-chat-approval-endpoint'></a>
```cs
chat.MapPost("/approvals", async Task<IResult> (
    int chatId,
    ApprovalAnswer[] answers,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    // No answers would drop every pending request
    if (answers.Length == 0) return TypedResults.BadRequest();

    var reply = new ChatMessage(ChatRole.User, [.. answers.Select(answer => answer.ToResponse())]);

    try
    {
        var resumed = await support.RunAsync(
            reply,
            settingsId: customer.Id,
            conversationId: chatId,
            ct: ct);

        return TypedResults.Ok(ChatReply.From(resumed));
    }
    catch (ApprovalClosedException)
    {
        // Answered in another tab, posted twice, or passed by a later message: the UI drops the prompt
        return TypedResults.Conflict();
    }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L56-L85' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-approval-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An answer to a request that has closed, for example one the user approved in another tab already, throws
`ApprovalClosedException` before the run calls the model. The endpoint answers it with 409 Conflict, so the UI can
drop the prompt ([When a request closes](./tool-approval.md#when-a-request-closes)).

`ToResponse` rebuilds each request from its ids and answers it, with a
[standing approval](./tool-approval.md#approving-for-the-conversation) of the tool when the user chose one:

<!-- snippet: approval-answer -->
<a id='snippet-approval-answer'></a>
```cs
internal sealed record ApprovalAnswer(
    string RequestId,
    string CallId,
    string Tool,
    bool Approved,
    bool ForConversation = false,
    string? Reason = null)
{
    // The call only fills in the request: Typeshift runs the call it stored under RequestId
    public ToolApprovalResponseContent ToResponse()
    {
        var request = new ToolApprovalRequestContent(RequestId, new FunctionCallContent(CallId, Tool));

        return Approved && ForConversation
            ? request.ApproveForConversation()
            : request.CreateResponse(Approved, Reason);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatContracts.cs#L28-L47' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-answer' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The `CallId` and `Tool` the UI sends back only fill in the request. Typeshift runs the call it stored under
  `RequestId` ([Approving or rejecting](./tool-approval.md#approving-or-rejecting)), and a standing approval goes to
  that call's tool. Decide on the stored call with an [approval rule](./tool-approval.md#approval-rules), never on the
  `Tool` the UI sent.
- Post an answer to every pending approval at once, since the next run closes each request it has no answer for, and
  clear the UI's pending approvals when the user sends a new message instead.
- The answering run passes the customer's settings id again
  ([Tracking a conversation](./conversations.md#tracking-a-conversation)).

::: warning A chat runs one answer at a time
A conversation has one run at a time in a process ([One run at a time](./conversations.md#one-run-at-a-time)), so a
second run of a chat throws an `InvalidOperationException` before it stores anything. That happens to a message posted
while an answer still streams, a double click on send, or answers posted before the run that asked has ended.
ASP.NET Core answers it with a 500 and logs an error. Disable the UI's send button while a run goes on, or pass a
message to the running answer with `TrySteer` ([Steering](./conversations.md#steering)). With several instances of
your app, nothing throws, so route each chat to one instance or keep its runs apart yourself.
:::

## Related

- [Using an agent](./using-an-agent.md): the host from a request's scope, a console app or a background job.
- [Streaming](./running-agents.md#streaming): what `RunStreamingAsync` returns.
- [Approval and modes](./tool-approval.md): the approval round trip, rules and modes.
- [Conversations](./conversations.md): what a chat stores, resuming, steering and live updates.
