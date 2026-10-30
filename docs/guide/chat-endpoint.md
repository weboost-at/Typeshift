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
that conversation, with the current customer's id as the settings id, so the customer's stored settings apply:

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
    var answer = await support.RunAsync(input.Text, settingsId: customer.Id, conversationId: chatId, ct: ct);
    return ChatReply.From(answer);
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L16-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The host and `ICurrentCustomer` come from the request's scope, as
  [Using an agent](./using-an-agent.md#inject-the-host-in-a-web-app) describes. `ICurrentCustomer` stands for a
  scoped service of your app.
- The `CancellationToken` is the request's, so the run is cancelled when the user leaves the page.
- `ChatReply` is the JSON the UI gets back: the answer's text and the approvals it waits for.

The filter on the group checks that the chat belongs to the caller before any of the group's endpoints runs, because
Typeshift resumes whichever conversation it is given
([Check the caller](./conversations.md#tracking-a-conversation)):

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

- `ApprovalRequests` lists the calls that wait for approval ([A run that asks](./tool-approval.md#a-run-that-asks)).
  `PendingApproval` keeps what the UI shows to ask, the tool and its arguments, and the ids it sends back with the
  user's answer.
- Show the arguments as the model sent them. Typeshift stored the call with those arguments, and that call is the one
  that runs once the user approves it.

## Stream the answer

The streaming endpoint runs the agent the same way, and hands its updates to `TypedResults.ServerSentEvents`:

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

    return TypedResults.ServerSentEvents(Events(updates, ct));
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L32-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each update becomes a `text` event with the text it adds, and each approval request an `approval` event with the
`PendingApproval` as JSON:

<!-- snippet: chat-stream-events -->
<a id='snippet-chat-stream-events'></a>
```cs
private static async IAsyncEnumerable<SseItem<object>> Events(
    IAsyncEnumerable<ChatResponseUpdate> updates,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var update in updates.WithCancellation(ct))
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L66-L82' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-events' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A browser reads the events with `fetch` and a reader over the response's body, because `EventSource` sends only GET
  requests.
- A run that asks for approval ends after its `approval` events, as it does without streaming. The UI then posts the
  user's answers to the approval endpoint.

## Answer the approvals

The UI posts the user's answer to each pending approval. The endpoint turns the answers into
`ToolApprovalResponseContent`s and runs the conversation's next run with them:

<!-- snippet: chat-approval-endpoint -->
<a id='snippet-chat-approval-endpoint'></a>
```cs
chat.MapPost("/approvals", async (
    int chatId,
    ApprovalAnswer[] answers,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    var reply = new ChatMessage(ChatRole.User, [.. answers.Select(answer => answer.ToResponse())]);

    var answer = await support.RunAsync(reply, settingsId: customer.Id, conversationId: chatId, ct: ct);
    return ChatReply.From(answer);
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L50-L63' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-approval-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each answer rebuilds the request from its ids and answers it, approving the tool for the rest of the chat when the
user asked for that ([Approving for the conversation](./tool-approval.md#approving-for-the-conversation)):

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
    // Typeshift runs the call it stored with the request, so the call here only names it
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

- Typeshift matches each answer to the request it stored by `RequestId`, and runs the call it stored with that
  request. The call `ToResponse` builds only names it, so a UI that sends changed arguments changes nothing.
- Post an answer to every pending approval at once, since the next run drops each request it has no answer for
  ([Approving or rejecting](./tool-approval.md#approving-or-rejecting)).
- The answering run passes the customer's settings id again, because a conversation does not remember one.

::: warning A message posted while an answer streams fails
A conversation has one run at a time in a process ([One run at a time](./conversations.md#one-run-at-a-time)). A
message posted to a chat while its answer still streams throws an `InvalidOperationException`, which ASP.NET Core
answers with a 500. Disable the UI's send button while an answer streams, or pass the message to the running answer
with `TrySteer` ([Steering](./conversations.md#steering)).
:::

## Related

- [Using an agent](./using-an-agent.md): the host from a request's scope, a console app or a background job.
- [Running agents](./running-agents.md#streaming): what a run takes and returns, and streaming.
- [Approval and modes](./tool-approval.md): the approval round trip, rules and modes.
- [Conversations](./conversations.md): what a chat stores, resuming, steering and live updates.
