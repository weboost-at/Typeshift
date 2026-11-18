---
title: Running an agent
description: Run an agent through its host with stored settings, run settings, typed output or streaming, call its chat client yourself, or use it in Agent Framework.
---

# Running an agent

You run an agent through its host, `AgentHost<TAgent>`: pass the user's message, and get back the model's answer with
the tool calls it made on the way. A run can apply a customer's stored settings, change them for one run, ask for a
typed answer, or stream as the model writes. [Using an agent](./using-an-agent.md) shows where your app gets the
host.

## Run an agent

<!-- snippet: run -->
<a id='snippet-run'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var support = scope.ServiceProvider.GetRequiredService<AgentHost<SupportAgent>>();

var answer = await support.RunAsync("Where is my refund?");

Console.WriteLine($"{answer.Messages[^1].AuthorName}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A run returns Microsoft.Extensions.AI's `ChatResponse`: the model's messages, with the tool calls and results between
them. Each message names the agent that wrote it in `AuthorName`.

The run takes its `IChatClient` from the run's services: the client your app registered under the provider the agent
names, or the default client when the agent names none. A provider nothing is registered for fails the run before it
starts. [Models and providers](./models.md) shows how to register the clients and how an agent names its model.

Every run takes a `CancellationToken` last, which reaches its model calls and its tools. Cancelling it stops the run
with an `OperationCanceledException`, so pass the token of the request or job that waits for the answer. On the
host's own members the token goes second, as in `RunAsync("Where is my refund?", ct)`, and a run with
[run settings](#run-settings) passes it after them: `RunAsync(input, settings: run, ct: ct)`. A run with a
conversation is stored up to where it stopped ([What is stored](./conversations.md#what-is-stored)).

[How a run works](./how-a-run-works.md) follows the steps every run passes through, streaming or not, from resolving
its options to the model's answer.

## Images and files

`RunAsync` also takes a `ChatMessage`, so the user's message can carry an image, a file or several parts, as
Microsoft.Extensions.AI's contents do:

<!-- snippet: run-message -->
<a id='snippet-run-message'></a>
```cs
var question = new ChatMessage(
    ChatRole.User,
    [
        new TextContent("Is this the charge on my statement?"),
        new DataContent(statement, "image/png")
    ]);

var charge = await support.RunAsync(question, ct);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L36-L45' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-message' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `statement` stands for the bytes of an image the user uploaded, and `DataContent` carries them with their media
  type. A file on the web goes in a `UriContent` instead.
- The provider and the model decide which kinds of content they read, so check that yours reads images before your
  UI offers an upload.

## Run any agent

A run without settings is a member of `AgentHost<TAgent>` itself, so a method generic over the agent runs any agent,
as a helper that serves every agent's endpoint or test does:

<!-- snippet: run-generic -->
<a id='snippet-run-generic'></a>
```cs
internal static class AgentAnswers
{
    public static async Task<string> AskAsync<TAgent>(AgentHost<TAgent> host, string question, CancellationToken ct)
        where TAgent : Agent
    {
        var answer = await host.RunAsync(question, ct);
        return $"{host.Agent.Name}: {answer.Text}";
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/AgentAnswers.cs#L3-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-generic' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `RunAsync(question, ct)` takes the token second. The runs, answers and resumes that take the agent's `Settings` are
  generated for each agent, since only the agent's code knows its record
  ([Typed members](./generated-members.md#typed-members)).
- `host.Agent.Name` is the agent's name, whichever agent the method runs.

## Stored settings

Give the host a settings id with `WithSettingsId`, and its runs layer the [settings stored](./settings-and-stores.md)
under it over the agent's code and its own stored settings. The call returns a copy of the host, as
[Ids of your own types](./settings-and-stores.md#ids-of-your-own-types) explains:

<!-- snippet: run-with-settings-id -->
<a id='snippet-run-with-settings-id'></a>
```cs
var settingsId = 42;

var stored = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L67-L71' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-with-settings-id' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Run settings

A run can also pass settings of its own, as an instance of the agent's generated
[`Settings`](./generated-members.md#settings) record. They override the stored settings for that run only:

<!-- snippet: run-settings -->
<a id='snippet-run-settings'></a>
```cs
var run = new SupportAgent.Settings
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Temperature = 0,
    SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true },
    SearchArticles = new KnowledgeToolset.SearchArticlesOverrides { Language = "de" }
};

var thorough = await support.WithSettingsId(settingsId).RunAsync("Where is my refund?", settings: run);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L75-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Values the run leaves null keep what the stored settings or the agent's code set, as
[settings layering](./settings-and-stores.md#settings-layering) explains.

## Structured output

`RunAsync<T>` asks the model for JSON in `T`'s schema and returns it typed, in `Result`:

<!-- snippet: structured-output -->
<a id='snippet-structured-output'></a>
```cs
var report = await support.WithSettingsId(settingsId).RunAsync<RefundStatus>("Summarize the refund.");

Console.WriteLine($"{report.Result.Invoice}: {report.Result.State}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L90-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: structured-output-type -->
<a id='snippet-structured-output-type'></a>
```cs
internal sealed record RefundStatus(string Invoice, RefundState State, DateOnly? Expected);

internal enum RefundState
{
    Requested,
    Approved,
    Paid
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L169-L178' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-output-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`T`'s XML docs describe its properties, as they describe the types tools take
([XML docs of parameter types](./registration.md#xml-docs-of-parameter-types)). Providers take only an object with
properties as the schema of an answer. So a `T` of another shape, such as a number, an enum, a list, a dictionary, a
polymorphic type or a [union](./toolsets.md#union-types), is asked for under a `data` property, and `Result` reads it
from there. Under
[Native AOT](./native-aot.md), `T` needs JSON metadata.

A member that System.Text.Json cannot read or write, such as an `Exception`, whose `TargetSite` is a `MethodBase`,
fails the run before it calls the model, naming the member. Leave such a member out of `T`, or mark it
`[JsonIgnore]`, since the model could not send one anyway. A tool's parameters fail the same way, and that message
names the tool and the parameter too.

A typed run returns whatever the model answered, and `Result` reads it when you first ask for it. So `Result` throws a
`JsonException` when the answer is no JSON of `T`, such as a model that answered in prose, and an
`InvalidOperationException` when the response holds no answer to read, such as a run that ends asking your app.
`TryGetResult` tells without throwing:

<!-- snippet: typed-run-try -->
<a id='snippet-typed-run-try'></a>
```cs
var chat = support.WithSettingsId(settingsId).WithConversationId(conversationId);

var closing = await chat.RunAsync<RefundStatus>("Close ticket 1234.");

if (closing.TryGetResult(out var status))
    Console.WriteLine($"{status.Invoice}: {status.State}");
else
    Console.WriteLine($"No result yet: {closing.Decisions.Count} decision waits for an answer.");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L100-L109' title='Snippet source file'>snippet source</a> | <a href='#snippet-typed-run-try' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The ticket's close waits for your app's [approval](./tool-approval.md), so the run has no result yet, and its
`Decisions` lists what it asks. `AnswerAsync<T>` and `AnswerStreamingAsync<T>` ask for `T` again when they continue
the run.

`RunStreamingAsync<T>` streams the typed answer as the model writes it. Read its updates to the end, and its `Response`
holds the result:

<!-- snippet: structured-streaming -->
<a id='snippet-structured-streaming'></a>
```cs
var summary = support.WithSettingsId(settingsId).RunStreamingAsync<RefundStatus>("Summarize the refund.");

await foreach (var update in summary)
    Console.Write(update.Text);

Console.WriteLine($" ({summary.Response.Result.State})");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L119-L126' title='Snippet source file'>snippet source</a> | <a href='#snippet-structured-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The updates carry the model's JSON as it writes it, under `data` for a `T` that is not an object, so show the user
`Result` rather than the streamed text. The model is called when you read the updates, and they can be read once: a
second `await foreach` throws an `InvalidOperationException`. `Response` throws one until the updates are read to the
end, and keeps throwing when the run failed, was cancelled or was left early.

Both typed runs also take a `ChatMessage` ([Images and files](#images-and-files)). An agent the run is
[handed off](./multi-agent.md) to answers in `T` too.

## Streaming

`RunStreamingAsync` returns the model's updates as they arrive. When the model calls tools, the tools run between its
updates, and the stream goes on with the model's next response:

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in support.WithSettingsId(settingsId).RunStreamingAsync("Where is my refund?"))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L130-L133' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Anyone [watching the conversation](./conversations.md#live-updates) sees the updates too.

The stream is the run: a `StreamingRun`, which calls the model as you read it, so you read it once. A second read
throws an `InvalidOperationException` instead of running the agent again. Once the updates are read to their end,
`Response` holds what they make, the decisions the run waits for included, as `RunAsync` would have returned it.

## Testing an agent

A test runs your agent on a fake model: an `IChatClient` of your own that answers from a script, so the test calls no
provider and gets the same answers on every run. This one answers each call with the next message of its script and
keeps what each call was sent:

<!-- snippet: scripted-chat-client -->
<a id='snippet-scripted-chat-client'></a>
```cs
// A model for tests: it answers each call with the next message of its script, and keeps what each call was sent
internal sealed class ScriptedChatClient(params ChatMessage[] script) : IChatClient
{
    private readonly Queue<ChatMessage> _script = new(script);

    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(([.. messages], options));
        return Task.FromResult(new ChatResponse(_script.Dequeue()));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(messages, options, cancellationToken);

        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/ScriptedChatClient.cs#L6-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-scripted-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The test hands it to `TypeshiftRuntime.Create` and runs the agent as your app would. The script's first message calls
the agent's tool, so Typeshift runs the tool and calls the model again with its result:

<!-- snippet: testing-an-agent -->
<a id='snippet-testing-an-agent'></a>
```cs
var arguments = new Dictionary<string, object?> { ["city"] = "Vienna" };

var model = new ScriptedChatClient(
    new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call_1", "find_companies", arguments)]),
    new ChatMessage(ChatRole.Assistant, "Found three companies in Vienna."));

await using var runtime = TypeshiftRuntime.Create(model);
await using var research = runtime.CreateHost<ResearchAgent>();

var answer = await research.RunAsync("Which companies are in Vienna?");

var sent = model.Calls[0].Options!;

var result = model.Calls[1].Messages
    .SelectMany(message => message.Contents)
    .OfType<FunctionResultContent>()
    .Single();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Testing.cs#L16-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-testing-an-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `answer` is what the run returns, with the script's last message in its `Text`.
- `sent` holds what the model was sent: the model in `ModelId`, the agent's instructions in `Instructions` and the
  tools the run offers in `Tools`. Assert on them to test what the agent declares and what a run's settings change.
- `result` is the tool's result as the model read it. A result that is a JSON object or array reaches the model as
  text, the JSON without white space ([Results](./toolsets.md#results)).
- The runtime keeps settings and conversations in memory, so the test needs no database, and each test gets a
  runtime of its own. The test project references the Typeshift package, as every project that calls
  `TypeshiftRuntime.Create` does ([Project setup](./registration.md#project-setup)).
- A tool is a static method, so a test of the tool alone calls it with the settings it takes:
  `CompanyToolset.FindCompanies("Vienna", new FindCompaniesSettings { Limit = 2 })`.
- Typeshift reads the time from the `TimeProvider` of the runtime's services, for example when it stores an entry,
  so a test that registers a fake one there, such as
  [`FakeTimeProvider`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.time.testing.faketimeprovider),
  sets the time itself ([The runtime](./registration.md#the-runtime)).
- A store of your own is tested against your database with `VerifyAsync`
  ([Check your store](./implementing-a-store.md#check-your-store)).

## The raw path

If your app needs the `IChatClient` itself, for example to hand it to a library that takes one, split a run into
three steps: resolve the options, create the client, and call the client with those options. Here `run` is the
`SupportAgent.Settings` from [Run settings](#run-settings):

<!-- snippet: raw-path -->
<a id='snippet-raw-path'></a>
```cs
var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync(settings: run);

using var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Where is my refund?")], options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L137-L142' title='Snippet source file'>snippet source</a> | <a href='#snippet-raw-path' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The client is the run's whole pipeline, the tool loop and the agent's run options included. It serves concurrent
calls. Each call takes its iteration limit from the options it is called with, while the error limit and the
concurrency of tool calls are those of the options the client was created with, so call a client with the options it
was created for. Disposing it does not dispose the client your provider returned.

To run in a conversation, resolve the options on a host with a conversation id, such as
`support.WithConversationId(chat).ResolveOptionsAsync()`, and every call with those options continues and stores that
conversation. On a host without one, `ChatOptions.ConversationId` names the conversation instead. It is a string, so
it names a conversation in your `IConversationStore<string>`, which suits a library that sets it itself. Typeshift
never sends the conversation's id to the provider.

## Use an agent in Agent Framework

[Microsoft Agent Framework](https://learn.microsoft.com/agent-framework/overview/agent-framework-overview) runs agents
of its type `AIAgent`, and its workflows and orchestrations combine them. Wrap the client of the raw path in one, and
your agent takes part in them. `AsAIAgent` and `ChatClientAgentOptions` come from the `Microsoft.Agents.AI` package:

<!-- snippet: agent-framework -->
<a id='snippet-agent-framework'></a>
```cs
var options = await support.WithSettingsId(settingsId).ResolveOptionsAsync();
using var client = await support.CreateChatClientAsync(options);

AIAgent agent = client.AsAIAgent(new ChatClientAgentOptions
{
    Name = support.Agent.Name,
    Description = support.Agent.Description,
    ChatOptions = options,
    UseProvidedChatClientAsIs = true
});

var answer = await agent.RunAsync("Where is my refund?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentFramework.cs#L18-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-framework' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `UseProvidedChatClientAsIs = true` keeps Agent Framework from wrapping the client in its default decorators, its
  own tool loop among them. The client runs Typeshift's already, with the agent's tools, settings and middleware.
- The options belong to one settings id, here a customer's, so create the `AIAgent` where you know the customer, such
  as in a request. Dispose the client only once you no longer run the agent.
- Resolve the options on a host with a conversation id, as for the raw path, and every run of the agent continues and
  stores that conversation. Typeshift keeps the messages, so an `AgentSession` of Agent Framework holds only the
  conversation's id. Without a conversation id, the session keeps the messages and Typeshift stores none.
- A run that waits for your app, for an approval or an answer of the user, returns its decisions among the response's
  messages. `answer.AsChatResponse().Decisions` lists them, and you answer them on the host with the conversation id,
  as [Approval and modes](./tool-approval.md#approving-or-rejecting) shows.

::: warning Pass no chat options per run
Agent Framework merges the `ChatOptions` of a run's `ChatClientAgentRunOptions` with the agent's into options of its
own, and Typeshift runs those without the host's conversation. Such a run stores nothing, or with a session takes the
session's id as a `string` conversation id, as the raw path does on a host without one. Put what changes per run into
the [run settings](#run-settings) you resolve the options with.
:::

## Related

- [Using an agent](./using-an-agent.md): getting the host in a web app, a console app or a background job.
- [How a run works](./how-a-run-works.md): the steps every run passes through, and what each one can change.
- [Models and providers](./models.md): the chat client provider and how agents name their models.
- [Agent host](./generated-members.md#the-agent-host): every member of the host and its signature.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Conversations](./conversations.md): storing, resuming and steering runs.
- [Serving a chat endpoint](./chat-endpoint.md): runs, streaming and decisions behind ASP.NET Core endpoints.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Architecture](../contributing/architecture.md#a-run): the same pipeline from the code's side.
- [Typeshift and Agent Framework](./what-is-typeshift.md#typeshift-and-agent-framework): what each library is for.
