# Running agents

A host runs an agent through its `AgentRuntime<TAgent>`: it resolves the `ChatOptions` of a run, layering settings
over the agent's code, then creates a chat client for the resolved model.

## Registration

<!-- snippet: registration -->
<a id='snippet-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift();
services.AddTypeshiftStore<InMemoryTypeshiftStore<Guid>, Guid>();

services.AddKeyedChatClient("fast-model", new CannedChatClient("fast-model"));
services.AddKeyedChatClient("reasoning-model", new CannedChatClient("reasoning-model"));
services.AddSingleton<IChatClientProvider, KeyedChatClientProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Host.cs#L13-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTypeshift()` registers every toolset and agent the project can see, `AddTypeshiftStore` the
[store](./persistence.md), and `IChatClientProvider` maps model names to clients.

## Chat clients

`IChatClientProvider` returns the `IChatClient` of a model name. Provider packages for Microsoft.Extensions.AI
register their clients with DI, so a provider over keyed registrations covers most hosts:

<!-- snippet: chat-client-provider -->
<a id='snippet-chat-client-provider'></a>
```cs
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult(services.GetRequiredKeyedService<IChatClient>(model));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Chat/KeyedChatClientProvider.cs#L7-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`CreateChatClientAsync(options)` asks the provider for the client of `options.ModelId` and wraps it: conversation
tracking outside, Microsoft.Extensions.AI's `FunctionInvokingChatClient` inside, so the agent's tools run in a loop
until the model answers. Options without a model throw a `ConfigurationRequiredException`.

## A run from code

`ResolveChatOptions()` builds the options from the agent's code alone and needs no store:

<!-- snippet: run-from-code -->
<a id='snippet-run-from-code'></a>
```cs
var support = services.GetRequiredService<AgentRuntime<SupportAgent>>();

var options = support.ResolveChatOptions();
var client = await support.CreateChatClientAsync(options);
var response = await client.GetResponseAsync("Where is my refund?", options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L15-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-from-code' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Call the client with the same options you created it with: they carry the agent's tools and its execution context.

## Run settings

A run can override what the agent's `Settings` allow, for that run only:

<!-- snippet: run-settings -->
<a id='snippet-run-settings'></a>
```cs
var run = new SupportAgent.Settings
{
    Model = "reasoning-model",
    Temperature = 0,
    SearchTickets = new TicketToolset.SearchTicketsOverrides { IncludeClosed = true }
};

var runOptions = support.ResolveChatOptions(run);
var runClient = await support.CreateChatClientAsync(runOptions);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L29-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The run's model, `reasoning-model`, picks a different client. Values a run leaves null keep what the layers below
set; see [the layers](./tool-settings.md#the-layers).

## Streaming

The client streams like any `IChatClient`; tool calls run between the streamed updates.

<!-- snippet: streaming -->
<a id='snippet-streaming'></a>
```cs
await foreach (var update in runClient.GetStreamingResponseAsync("Where is my refund?", runOptions))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L43-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-streaming' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## A run with stored settings

`ResolveChatOptionsAsync(id)` also layers the agent's settings and its tools' defaults stored under an id; see
[stores and settings](./persistence.md#agent-settings). Pass `conversationId` to track the run as part of a
[conversation](./conversations.md).
