---
title: Serving a chat endpoint
description: Run an agent from ASP.NET Core endpoints, stream its answer to a chat UI as server-sent events or AG-UI events, and answer the decisions a run waits for by their ids.
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
    .AddEndpointFilter<ChatOwnerFilter>()
    .AddEndpointFilter<ChatBusyFilter>();

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
        .RunAsync(input.Text, ct);

    return ChatReply.From(response);
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L19-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The host and `ICurrentCustomer` come from the request's scope, as
  [Using an agent](./using-an-agent.md#inject-the-host-in-a-web-app) describes. `ICurrentCustomer` stands for a
  scoped service of your app, filled from the request's authentication, so put your authentication on the group too,
  for example with `RequireAuthorization()`.
- The `CancellationToken` is the request's, so the run is cancelled when the user leaves the page. A run that should
  finish anyway takes no token.
- `ChatReply` is the JSON the UI gets back: the answer's text and the decisions it waits for.
- When the provider fails, the conversation holds the message already, so the UI's "Retry" posts to an endpoint that
  calls `RetryAsync` instead of sending the message again
  ([Retrying a failed run](./conversations.md#retrying-a-failed-run)).
- The settings id is the customer's own, never a value of the request, and the body carries only the text. Settings
  can set every member the agent made configurable, the model, the instructions and the mode among them, so a
  customer changes theirs through a form that holds only what they may change
  ([Settings from a request](./settings-and-stores.md#settings-from-a-request)).

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
reply returns it as it is: its `kind`, `approval`, `plan` or `question`, its `id`, which the UI sends back with the
user's answer, and what the UI shows to ask. That is the `tool` with its `arguments`, the `plan`, or the `questions`
the model [asks the user](./tool-approval.md#asking-the-user), each with its `header`, its `text`, its `options`
(a `label` and a `description` each) and `multiSelect`. Show the arguments as they are, because those are the
arguments the call runs with.

## Stream the answer

The streaming endpoint runs the agent the same way, and hands its updates to
[`TypedResults.ServerSentEvents`](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/responses):

<!-- snippet: chat-stream-endpoint -->
<a id='snippet-chat-stream-endpoint'></a>
```cs
chat.MapPost("/messages/stream", async (
    int chatId,
    ChatInput input,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    ILogger<SupportAgent> log,
    CancellationToken ct) =>
{
    var updates = support
        .WithSettingsId(customer.Id)
        .WithConversationId(chatId)
        .RunStreamingAsync(input.Text, ct);

    return TypedResults.ServerSentEvents(Events(await StartedAsync(updates, ct), log, ct));
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L40-L56' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each update becomes a `text` event with the text it adds, and each decision a `decision` event. The stream ends with
a `done` event, or with an `error` event when the run failed:

<!-- snippet: chat-stream-events -->
<a id='snippet-chat-stream-events'></a>
```cs
private static async IAsyncEnumerable<SseItem<object>> Events(
    IAsyncEnumerable<ChatResponseUpdate> updates,
    ILogger log,
    [EnumeratorCancellation] CancellationToken ct)
{
    await using var reader = updates.GetAsyncEnumerator(ct);
    var failed = false;

    while (true)
    {
        try
        {
            if (!await reader.MoveNextAsync()) break;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // The response is under way, so the page learns of the failure from an event, and the exception,
            // whose message can name your database or your provider, goes to the log
            log.LogError(exception, "An answer to a chat failed after it began.");
            failed = true;
            break;
        }

        var update = reader.Current;

        if (update.Text is { Length: > 0 } text) yield return new SseItem<object>(text, "text");

        // Decisions come with the run's last update, so each one the UI shows can be answered
        foreach (var decision in update.Decisions)
        {
            yield return new SseItem<object>(decision, "decision");
        }
    }

    // The last event: a stream that ends without one was cut off, by the network for example
    yield return new SseItem<object>("", failed ? "error" : "done");
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L131-L169' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-events' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Every event carries JSON, written with your app's JSON options: a `text` event holds its text as a JSON string, and a
  `decision` event holds the decision as the reply does. `done` and `error` hold an empty string, since the event's
  name says what happened. Parse each event's data with `JSON.parse`.
- A browser reads the events with `fetch` and a reader over the response's body, because
  [`EventSource`](https://developer.mozilla.org/docs/Web/API/EventSource) sends only GET requests.
- A run that asks sends its `decision` events once it has ended, just before `done`, so the UI can post an answer the
  moment it shows the prompt. A reader that left the stream before then gets the decisions from the
  [endpoint below](#load-the-decisions).
- `done` ends the stream of every run that ended, whether it answered or asks your app. A stream that stops without
  `done` or `error` was cut off, by the network for example, so the page loads the chat again
  ([Load the chat](#load-the-chat)) rather than taking the text so far for the whole answer.
- `error` ends the stream of a run that failed after its response began, such as a provider that drops the connection
  halfway through an answer. The event leaves the exception out, because its message can name your database or your
  provider, and the endpoint logs it instead. The conversation holds the user's message already, so the page offers
  to retry ([Retrying a failed run](./conversations.md#retrying-a-failed-run)) instead of sending it again.
- A run that fails at its first update fails the request instead, since the endpoint reads that update before the
  response begins ([A chat that is answering](#a-chat-that-is-answering)). ASP.NET Core then answers 500 and logs the
  exception, as it does for the endpoint that does not stream.
- The response reads the run's stream, so when the user leaves the page, ASP.NET stops reading and the run stops at
  its next update, stored up to there, with or without a token. A run that has to finish either way runs as a
  [background job](./using-an-agent.md#run-from-a-background-job), and the page follows it as below: a page that
  joins while it answers receives the answer from its start, and `EndsRun` tells it the run ended.

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
- In a hub, the current customer comes from the connection's user, so `ICurrentCustomer` reads it from there.
- The browser calls `connection.stream("Watch", chatId)`. SignalR cancels `ct` when the browser disposes its
  subscription or the connection drops, which ends the loop.
- A page that opens a chat shows its stored history first ([Load the chat](#load-the-chat)) and then watches. A
  watch begins with what the run going on published that the store does not hold yet, so an answer that was
  streaming when the page opened shows from its start.

Your app registers SignalR with its services and maps the hub next to the chat endpoints:

<!-- snippet: conversation-hub-map -->
<a id='snippet-conversation-hub-map'></a>
```cs
builder.Services.AddSignalR();

await using var app = builder.Build();

app.MapHub<ConversationHub>("/hub");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/ChatEndpoint.cs#L47-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation-hub-map' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Map it behind your authentication, as the endpoints are, for example with `RequireAuthorization()` after `MapHub`.

## Load the chat

A page that opens a chat, or reloads, shows the lines the chat holds so far. `LoadConversationAsync` reads the chat's
entries without a run, and the endpoint returns what the user reads of them:

<!-- snippet: chat-history-endpoint -->
<a id='snippet-chat-history-endpoint'></a>
```cs
chat.MapGet("/messages", async (int chatId, AgentHost<SupportAgent> support, CancellationToken ct) =>
    ChatLine.From(await support.WithConversationId(chatId).LoadConversationAsync(ct)));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L58-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-history-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: chat-line -->
<a id='snippet-chat-line'></a>
```cs
// A line of the chat as its user reads it: the user's messages and the agents' answers, each once
internal sealed record ChatLine(string Role, string Text)
{
    public static IEnumerable<ChatLine> From(IReadOnlyList<ConversationEntry> entries) =>
        entries
            .Where(entry => !entry.IsSummary)
            .SelectMany(entry => entry.Messages)
            .Where(message => message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
            .Where(message => !message.IsBackgroundAnswer)
            .Where(message => message.Text.Length > 0)
            .Select(message => new ChatLine(message.Role.Value, message.Text));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatContracts.cs#L15-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-line' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ChatLine` is a record of your own, as the reply is, and `From` keeps the lines a chat page shows
([What a chat page shows](./conversations.md#what-a-chat-page-shows)). What the last answer asked comes from the
next endpoint.

## Load the decisions

After a reload the page has lost what the last answer asked. `GetDecisionsAsync` reads it again, from the chat's
decisions alone, without a run:

<!-- snippet: chat-decisions-endpoint -->
<a id='snippet-chat-decisions-endpoint'></a>
```cs
chat.MapGet("/decisions", async (int chatId, AgentHost<SupportAgent> support, CancellationToken ct) =>
    (await support.WithConversationId(chatId).GetDecisionsAsync(ct)).Open);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L63-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-decisions-endpoint' title='Start of snippet'>anchor</a></sup>
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
            .AnswerAsync([answer], ct);

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L68-L92' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-answer-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The body is a `DecisionAnswer`. `{"decisionId":"...","approved":true}` approves, `"approved":false` with a
  `"reason"` rejects and tells the model why, `"forConversation":true` makes the approval a
  [standing approval](./tool-approval.md#approving-for-the-conversation), which covers every later call of the tool
  until your app withdraws it, and `"mode":"Auto"` picks the mode an approved
  [plan](./tool-approval.md#plan-mode) continues in.
- Questions are answered with `"answers"`, one entry for each question in the questions' order:
  `{"decisionId":"...","answers":[{"selected":["It leaks"]},{"text":"Since Monday."}]}`. An entry holds the labels
  the user picked in `selected`, their own words in `text`, or both. Without `answers`, the body declines the
  questions, and its `"reason"` tells the model why.
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

## A chat that is answering

A conversation has one run at a time in a container ([One run at a time](./conversations.md#one-run-at-a-time)), so a
second run of a chat throws a `ConversationBusyException` before it stores anything. That happens to a message posted
while an answer still streams, to a double click on send, and to a retry. With several instances of your app, your
store refuses a second run's entries, and the run that appends second throws the same exception, at its first step
after the other run's ([Running on several instances](./conversations.md#running-on-several-instances)). A
filter on the group answers it with 409 Conflict, where ASP.NET Core would answer 500 and log an error:

<!-- snippet: chat-busy-filter -->
<a id='snippet-chat-busy-filter'></a>
```cs
internal sealed class ChatBusyFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ConversationBusyException)
        {
            // An answer to this chat is under way: a double click on send, a second tab or a retry
            return TypedResults.Conflict();
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatBusyFilter.cs#L5-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-busy-filter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The group in [Post a message](#post-a-message) adds it after the owner filter, so another customer's chat still
  answers 404 and tells the caller nothing.
- The UI keeps the message and offers to send it again, or passes it to the running answer with `TrySteer`
  ([Steering](./conversations.md#steering)). Disabling the send button while a run goes on saves most of these.
- The last answer to a decision posted while a run of the chat goes on is recorded first and then answers 409 the
  same way. Post it again once that run has ended, and the chat continues.

A streaming run starts when its first update is read, and by then a streaming endpoint has begun its response, so a
busy chat would cut the stream off instead of answering 409. The streaming endpoint above therefore reads the first
update before it hands the updates on, and the filter sees the refusal first:

<!-- snippet: chat-stream-start -->
<a id='snippet-chat-stream-start'></a>
```cs
private static async Task<IAsyncEnumerable<ChatResponseUpdate>> StartedAsync(
    IAsyncEnumerable<ChatResponseUpdate> updates,
    CancellationToken ct)
{
    var reader = updates.GetAsyncEnumerator(ct);

    try
    {
        // The run starts with its first update: one that cannot start fails here, before the response begins
        return Rest(reader, await reader.MoveNextAsync());
    }
    catch
    {
        await reader.DisposeAsync();
        throw;
    }
}

private static async IAsyncEnumerable<ChatResponseUpdate> Rest(
    IAsyncEnumerator<ChatResponseUpdate> reader,
    bool any)
{
    await using (reader)
    {
        for (; any; any = await reader.MoveNextAsync())
        {
            yield return reader.Current;
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.cs#L98-L129' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-stream-start' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Serve an AG-UI client

[AG-UI](https://github.com/ag-ui-protocol/ag-ui) is an open protocol between agents and the UIs that show them. A
frontend built on an AG-UI client talks to your agent through one endpoint, with no wire format of your own to
design. The AG-UI .NET SDK turns a run's updates into AG-UI events: with its
[`AGUI.Server`](https://www.nuget.org/packages/AGUI.Server) package, version 1.0.1, one more endpoint of the chat
group serves such a client:

<!-- snippet: ag-ui-endpoint -->
<a id='snippet-ag-ui-endpoint'></a>
```cs
chat.MapPost("/ag-ui", async Task<IResult> (
    int chatId,
    HttpRequest request,
    AgentHost<SupportAgent> support,
    ICurrentCustomer customer,
    CancellationToken ct) =>
{
    var input = (await request.ReadFromJsonAsync<RunAgentInput>(AgUiJson, ct))!;
    var context = input.ToChatRequestContext(AgUiJson, AgUiStream);
    var host = support.WithSettingsId(customer.Id).WithConversationId(chatId);

    // The conversation holds the chat, so a run takes the user's last message or the answers to interrupts
    var updates = input.Resume is { Count: > 0 } resume
        ? host.AnswerStreamingAsync([.. resume.Select(Answer)], ct)
        : host.RunStreamingAsync(context.Messages[^1], ct);

    try
    {
        var events = (await StartedAsync(updates, ct))
            .AsAGUIEventStreamAsync(context, ct)
            .Select(@event => JsonSerializer.Serialize(@event, AgUiJson));

        return TypedResults.ServerSentEvents(events);
    }
    catch (DecisionClosedException)
    {
        // Answered already, in another tab or by a retry: the UI drops the interrupt
        return TypedResults.Conflict();
    }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.AgUi.cs#L44-L75' title='Snippet source file'>snippet source</a> | <a href='#snippet-ag-ui-endpoint' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The endpoint sits on the chat group, so the owner filter checks the chat and the busy filter answers a second run
  with 409, as for the other endpoints. The conversation id is the chat's, from the route: the client posts to the
  chat's address, such as `/chats/42/ag-ui`, with the chat's id as its thread id, which the events repeat.
- An AG-UI client sends the whole conversation with every run. Typeshift keeps the conversation under the chat's id,
  so the endpoint runs only the last message, and a run that answers interrupts carries out the answers instead.
- `StartedAsync` reads the first update before the response begins, as in
  [A chat that is answering](#a-chat-that-is-answering). Each event goes out as one line of JSON in a server-sent
  event.

Two settings make AG-UI's conversion work with Typeshift's runs, the JSON options and the mapping of a decision:

<!-- snippet: ag-ui-options -->
<a id='snippet-ag-ui-options'></a>
```cs
// AG-UI's types first, then Microsoft.Extensions.AI's and the content that carries a decision
internal static readonly JsonSerializerOptions AgUiJson = CreateAgUiJson();

private static readonly AGUIStreamOptions AgUiStream = new AGUIStreamOptions().MapInterrupt(Interrupt);

private static JsonSerializerOptions CreateAgUiJson()
{
    var json = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions) { WriteIndented = false };
    json.TypeInfoResolverChain.Insert(0, AGUIJsonUtilities.DefaultTypeInfoResolver);
    AIJsonUtilities.AddAIContentType<DecisionRequestContent>(json, "decision");
    json.MakeReadOnly();
    return json;
}

// A decision ends the run as an interrupt with the decision's id, and the decision's JSON as its metadata
private static AGUIInterrupt? Interrupt(AIContent content) =>
    content is DecisionRequestContent { Decision: var decision }
        ? new AGUIInterrupt
        {
            Id = decision.Id,
            Reason = "decision",
            Metadata = JsonSerializer.SerializeToElement(decision, AgUiJson)
        }
        : null;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.AgUi.cs#L15-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-ag-ui-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Each event carries the update it came from as its raw event, and a tool call's events carry its arguments and
  result, all of them Microsoft.Extensions.AI's types. So the JSON options resolve those as well as AG-UI's own. With
  AG-UI's context alone, even an answer without a tool call ends in `RUN_ERROR`.
- The last update of a run that asks holds a `DecisionRequestContent`, the content that carries a decision, which
  Microsoft.Extensions.AI's JSON does not know. The options add it, or that run ends in `RUN_ERROR` too.
- `MapInterrupt` turns each decision into an AG-UI interrupt, so the run ends with `RUN_FINISHED` and the outcome
  `interrupt`. The interrupt's metadata is the decision's JSON as the [reply](#return-the-answer) holds it, so the UI
  shows the same prompt as the chat UI.

The client answers the interrupts on its next run, in the `resume` of its input, and the endpoint turns each into a
`DecisionAnswer`:

<!-- snippet: ag-ui-answer -->
<a id='snippet-ag-ui-answer'></a>
```cs
// A resolved interrupt's payload is the answer endpoint's body without the decision's id: {"approved":true}
private static DecisionAnswer Answer(AGUIResume resume) =>
    resume is { Status: ResumeStatus.Resolved, Payload: { } payload }
        ? payload.Deserialize<DecisionAnswer>(AgUiJson)! with { DecisionId = resume.InterruptId }
        : DecisionAnswer.Reject(resume.InterruptId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Web/ChatEndpoints.AgUi.cs#L78-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-ag-ui-answer' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The payload of a resolved interrupt is the body that [Answer a decision](#answer-a-decision) takes, so questions are
  answered with `answers` as there. A cancelled interrupt rejects the call or the plan, or declines the questions.
- `AnswerStreamingAsync` records the answers and, once no decision of the chat is left open, streams the rest of the
  run: the approved call's result and the answer. An interrupt answered again, in another tab for example, answers
  409 Conflict.

::: info What the endpoint leaves out
The agent runs with its own tools and settings, so the endpoint ignores the tools, the state and the context an AG-UI
client sends. AG-UI's frontend tools, which the browser runs, and the state it shares with the UI are not covered.
The interrupts carry Typeshift's decisions in their metadata rather than AG-UI's payload for a tool approval, so a
UI reads the decision from there. A run that fails after its first update ends in `RUN_ERROR` with AG-UI's own
message, which leaves the exception out, and the run's span records it ([Observability](./observability.md#spans)).
:::

## Related

- [Using an agent](./using-an-agent.md): the host from a request's scope, a console app or a background job.
- [Streaming](./running-agents.md#streaming): what `RunStreamingAsync` returns.
- [Approval and modes](./tool-approval.md): decisions, their answers, rules and modes.
- [Conversations](./conversations.md): what a chat stores, resuming, steering and live updates.
