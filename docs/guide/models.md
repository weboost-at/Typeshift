---
title: Models and providers
description: Name an agent's model by provider and model, return its client from your IChatClientProvider, configure providers and keys per customer, and handle a provider that fails.
---

# Models and providers

An agent names its model by a provider and a model, and your `IChatClientProvider` returns the client for that pair.
Typeshift sends each provider its own model id, so your app never builds composite names like `openai/gpt-5-mini`.
A model a user picks is stored like any other setting.

## The chat client provider

You implement one method. Typeshift calls it for every run with the provider and the model the run resolved, and again
when [compaction](./conversations.md#compaction) needs the summary model:

<!-- snippet: chat-client-provider -->
<a id='snippet-chat-client-provider'></a>
```cs
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    // One client per provider serves all its models: Typeshift puts the model into every request
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(
            services.GetKeyedService<IChatClient>(provider)
                ?? throw new InvalidOperationException($"No IChatClient is registered for the provider '{provider}'."));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/KeyedChatClientProvider.cs#L6-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift puts the model into every request as `ChatOptions.ModelId`, which is why the sample ignores its `model`
parameter and returns one client per provider. Register the provider on the chain, and register your clients however
you like, as long as none runs a tool loop of its own ([Wrapping a client](#wrapping-a-client) says why). Here
`openAi` and `anthropic` stand for the `IChatClient` instances your app creates, one per provider:

<!-- snippet: chat-client-provider-registration -->
<a id='snippet-chat-client-provider-registration'></a>
```cs
services.AddTypeshift()
    .WithChatClientProvider<KeyedChatClientProvider>();

services.AddKeyedSingleton("openai", openAi);
services.AddKeyedSingleton("anthropic", anthropic);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L17-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: info The provider is scoped
Typeshift resolves your provider from each run's scope, so it can use that request's services, such as the current
customer's credentials or your `DbContext`. Typeshift never disposes a client you return, and it instruments each
provider and model once, whatever instance you return. So you may return a client per customer, and a client you
create for a single run is yours to dispose.
:::

If no provider is registered, a run throws an `InvalidOperationException` that tells you to call
`WithChatClientProvider<T>()`.

## A real client

Each provider's client library has an adapter to `IChatClient` for Microsoft.Extensions.AI. For OpenAI, add the
`Microsoft.Extensions.AI.OpenAI` package:

```bash
dotnet add package Microsoft.Extensions.AI.OpenAI
```

Then register one client for the provider:

<!-- snippet: openai-client -->
<a id='snippet-openai-client'></a>
```cs
var openAi = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();

services.AddKeyedSingleton("openai", openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L28-L34' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Typeshift names the model in every request as `ChatOptions.ModelId`, so this one client serves every OpenAI model
  your agents name. The model you pass to `GetChatClient` only names the client.
- `OpenAIClient` is in the `OpenAI` namespace. Read the API key from your app's configuration, never from code.
- Register the client as the adapter returns it, or wrapped in the middleware that
  [Wrapping a client](#wrapping-a-client) allows.

A local server with an OpenAI-compatible API, such as LM Studio or Ollama, works with the same package. Point the
client at its endpoint, and name its models in `WithModel`, here as `WithModel("local", "qwen3-8b")`.
`ApiKeyCredential` is in the `System.ClientModel` namespace:

<!-- snippet: local-chat-client -->
<a id='snippet-local-chat-client'></a>
```cs
var options = new OpenAIClientOptions
{
    Endpoint = new Uri("http://localhost:1234/v1")
};

var local = new OpenAIClient(new ApiKeyCredential("none"), options)
    .GetChatClient("qwen3-8b")
    .AsIChatClient();

services.AddKeyedSingleton("local", local);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L39-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-local-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For other providers, such as Anthropic or Azure OpenAI, register the `IChatClient` of their own package.
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) lists them. A fake client for
tests reads the same requests: the model in `ChatOptions.ModelId`, the agent's instructions in
`ChatOptions.Instructions` and the run's tools in `ChatOptions.Tools`. It calls a tool by answering with a
`FunctionCallContent`, and Typeshift runs the call.

## Providers from configuration

Your app can read its providers from configuration instead of code, so adding a provider or moving one to another
endpoint changes only `appsettings.json`:

```json
{
  "Providers": {
    "openai": {},
    "local": { "Endpoint": "http://localhost:1234/v1", "ApiKey": "none" }
  }
}
```

Each entry under `Providers` becomes one keyed client, here for providers with an OpenAI-compatible API:

<!-- snippet: providers-from-configuration -->
<a id='snippet-providers-from-configuration'></a>
```cs
foreach (var provider in configuration.GetSection("Providers").GetChildren())
{
    var options = new OpenAIClientOptions();
    if (provider["Endpoint"] is { } endpoint) options.Endpoint = new Uri(endpoint);

    var key = provider["ApiKey"]
        ?? throw new InvalidOperationException($"Providers:{provider.Key}:ApiKey is not configured.");

    var client = new OpenAIClient(new ApiKeyCredential(key), options)
        .GetChatClient("default")
        .AsIChatClient();

    services.AddKeyedSingleton(provider.Key, client);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L55-L70' title='Snippet source file'>snippet source</a> | <a href='#snippet-providers-from-configuration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The entry's name, such as `openai`, is the provider your agents name in `WithModel` and your users pick in their
  settings, so the `KeyedChatClientProvider` above finds each client by it.
- Keep the keys out of `appsettings.json`. ASP.NET Core reads `Providers:openai:ApiKey` from user secrets in
  development and from the environment variable `Providers__openai__ApiKey` in production, as it reads every setting.
- A provider without a key fails at startup, so a missing secret never surfaces in the middle of a run. A local server
  takes any key, such as `none`.

## A key per customer

When each customer brings their own API key, the client depends on who is asking. Typeshift resolves your provider
from each run's scope, so it can read the current customer, and a singleton keeps one client per customer and
provider:

<!-- snippet: tenant-chat-client-provider -->
<a id='snippet-tenant-chat-client-provider'></a>
```cs
internal sealed class TenantChatClientProvider(ICurrentCustomer customer, TenantClients clients) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(clients.Get(customer.Id, provider));
}

// A singleton: a client keeps its connections open, so one per run would open new ones on every run
internal sealed class TenantClients(ITenantKeys keys) : IDisposable
{
    private readonly ConcurrentDictionary<(int Customer, string Provider), IChatClient> _clients = [];

    public IChatClient Get(int customer, string provider) =>
        _clients.GetOrAdd((customer, provider), key => new OpenAIClient(keys.GetApiKey(key.Customer, key.Provider))
            .GetChatClient("default")
            .AsIChatClient());

    // Call it when a customer changes a key, so the next run builds a client with the new one
    public void Forget(int customer, string provider)
    {
        if (_clients.TryRemove((customer, provider), out var client)) client.Dispose();
    }

    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/TenantChatClientProvider.cs#L7-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-tenant-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register the provider on the chain, and its cache as a singleton:

<!-- snippet: tenant-chat-client-provider-registration -->
<a id='snippet-tenant-chat-client-provider-registration'></a>
```cs
services.AddTypeshift()
    .WithChatClientProvider<TenantChatClientProvider>();

services.AddSingleton<TenantClients>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L75-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-tenant-chat-client-provider-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `ICurrentCustomer` and `ITenantKeys` stand for services of your app: who the request is for, and where you keep the
  keys your customers entered.
- A client per run works too, since Typeshift never disposes a client you return, but every run would open new
  connections to the provider. The cache keeps one client per customer and provider for as long as your app runs.
- A cached client keeps the key it was created with. When a customer changes their key, call `Forget`, so the next run
  creates a client with the new one.

## Wrapping a client

You may wrap a provider's client in Microsoft.Extensions.AI middleware before you register it, for example to log each
request:

<!-- snippet: wrapped-chat-client -->
<a id='snippet-wrapped-chat-client'></a>
```cs
var openAi = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient()
    .AsBuilder()
    .UseLogging(loggers)
    .Build();

services.AddKeyedSingleton("openai", openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L85-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-wrapped-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Wrap what concerns a single request, such as logging, caching or a policy of your own written as a
`DelegatingChatClient`. Two kinds of middleware repeat what Typeshift already does:

- `UseFunctionInvocation()` runs a second tool loop inside the one Typeshift runs, and a run that answers an
  [approval request](./tool-approval.md) fails.
- `UseOpenTelemetry()` writes a second span for every model call, because Typeshift instruments each client itself,
  with `chat {model}` spans on its `Typeshift` source ([Spans](./production.md#spans)).

Leave retries to the client library, which knows which of its errors are worth another try. The OpenAI client retries
timeouts, rate limits and server errors on its own, and its `RetryPolicy` sets how often. `ClientRetryPolicy` is in
the `System.ClientModel.Primitives` namespace:

<!-- snippet: client-retries -->
<a id='snippet-client-retries'></a>
```cs
var options = new OpenAIClientOptions
{
    RetryPolicy = new ClientRetryPolicy(maxRetries: 5)
};

var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), options);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L99-L106' title='Snippet source file'>snippet source</a> | <a href='#snippet-client-retries' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The provider id type

A provider is a string unless you choose another type. Set `TypeshiftProviderIdType` once for the build, in
`Directory.Build.props`, as you set the [id type](./registration.md#the-id-type):

<!-- snippet: provider-id-type -->
<a id='snippet-provider-id-type'></a>
```csproj
<PropertyGroup>
  <TypeshiftProviderIdType>Typeshift.Samples.Chat.Provider</TypeshiftProviderIdType>
</PropertyGroup>
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Typeshift.Samples.Console.csproj#L20-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-id-type' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Because the agent's `Settings` store a provider as a single value, the type must be a string, a number, a `Guid` or
an enum. A `Guid` suits providers that are rows in your database, and an enum suits providers known in code: the
snippet names the enum `Typeshift.Samples.Chat.Provider`, so the compiler checks every provider you name, as in
`WithModel(Provider.Scripted, "scripted-model")`.

Your provider class still implements `IChatClientProvider`. The generator writes that name as an alias for your
provider id type, so `GetChatClientAsync` takes a provider of your type instead of a string. A public agent with a
configurable model names the type in its `Settings`, so the type must be public too
([TSH006](./diagnostics.md#tsh006)). The analyzer reports a provider of another type
([TSH030](./diagnostics.md#tsh030)), a type it cannot use ([TSH029](./diagnostics.md#tsh029)), and a referenced
project built with another provider id type ([TSH021](./diagnostics.md#tsh021)).

## Naming a model

`WithModel` takes the provider first, then the model as that provider's API names it:

<!-- snippet: agent-model -->
<a id='snippet-agent-model'></a>
```cs
WithChatOptions()
    .WithModel("openai", "gpt-5-mini")
    .WithTemperature(0.3f)
    .Configurable(o => o.ModelId)
    .Configurable(o => o.Instructions)
    .Configurable(o => o.Temperature);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-model' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Configurable(o => o.ModelId)` adds `Provider` and `Model` to the agent's
[`Settings`](./generated-members.md#settings), so stored settings or a run can pick another model. The two values
[layer](./settings-and-stores.md#settings-layering) separately: a layer that sets only `Model` keeps the provider of
the layer below, so set both when you switch providers.

Leave out `WithModel` but keep `Configurable(o => o.ModelId)`, and the choice is the user's. Every run then needs a
provider and a model from stored settings or from the run's own settings, and a run without them throws an
`InvalidOperationException` that names the agent. Store the pick under a settings id, from a settings page that
offers your providers and their models, for example:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
await support.ConfigureAsync(settingsId, settings => settings with
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L14-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The summary model

[Compaction](./conversations.md#compaction) summarizes the oldest turns with the run's own model unless you name
another. In code you name only the model, and the run's provider serves it:

<!-- snippet: long-running-options -->
<a id='snippet-long-running-options'></a>
```cs
WithRunOptions()
    .WithMode(AgentMode.Plan)
    .WithMaxToolIterations(500)
    .WithMaxInputTokens(150_000)
    .WithSummaryModel("claude-haiku-5-5")
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations)
    .Configurable(r => r.SummaryModel);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L20-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

With `Configurable(r => r.SummaryModel)`, stored settings or a run may name another provider for it too:

<!-- snippet: summary-provider -->
<a id='snippet-summary-provider'></a>
```cs
var settings = new CodingAgent.Settings
{
    SummaryProvider = "openai",
    SummaryModel = "gpt-5-mini"
};
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L113-L119' title='Snippet source file'>snippet source</a> | <a href='#snippet-summary-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Named | The summary runs on |
|---|---|
| Nothing | The run's provider and model |
| `WithSummaryModel(model)`, or `SummaryModel` in settings | The run's provider, that model |
| `SummaryProvider` and `SummaryModel` in settings | That provider, that model |

::: warning The summary model follows the run's provider
The run's provider is whichever provider the layers resolved for that run, so it changes when a user switches the
agent to another provider. The summary model named in code then goes to that provider too, and if that provider
cannot serve the model, the run fails. Make the summary model configurable wherever users switch providers.
:::

## When a provider fails

What your code sees depends on whether the run fails before or after it calls the model:

| What fails | Your code gets | The run's conversation |
|---|---|---|
| No chat client provider is registered | An `InvalidOperationException` that names `WithChatClientProvider<T>()` | Unchanged |
| The run resolved no provider or no model | An `InvalidOperationException` that names the agent | Unchanged |
| Your provider throws, such as for a provider it does not know | Your provider's exception | Unchanged |
| The model call fails, such as on a wrong key, an unknown model or a rate limit that outlasts the retries | The client library's exception, such as OpenAI's `ClientResultException` | Ends with an entry that holds an `ErrorContent` |

- Typeshift asks your provider for the client before the run starts, so the first three fail before anything is
  stored, and you can run the agent again once you fixed the cause.
- A model call that fails ends the run ([What is stored](./conversations.md#what-is-stored)), and the run's
  `invoke_agent` span carries the exception's type as `error.type` ([Spans](./production.md#spans)). A streaming run
  throws the same exception from the loop over its updates.
- A model a user picked is checked only when a run calls it, so a typo saved on a settings page fails a later run.
  Check the pick against the models the provider offers when your settings page saves it.

## Related

- [Running agents](./running-agents.md): the run's services and the steps of a run.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Builders](./builders.md#chatoptionsbuilder): every chat and run option.
- [In production](./production.md#observability): the spans and metrics of each model call.
