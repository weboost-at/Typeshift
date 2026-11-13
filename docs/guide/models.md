---
title: Models and providers
description: Register the chat client of each provider, name an agent's model by provider and model, serve keys per customer from your own provider, handle a failing provider, and pick the summary model.
---

# Models and providers

An agent names its model by a provider and a model, such as `WithModel("openai", "gpt-5-mini")`. Typeshift finds the
`IChatClient` your app registered for that provider and sends it the model as `ChatOptions.ModelId`, so one client
serves every model of its provider. A model a user picks is stored like any other setting.

## Registering clients

Register one client per provider, with the provider's name as its key:

<!-- snippet: keyed-chat-clients -->
<a id='snippet-keyed-chat-clients'></a>
```cs
services.AddKeyedChatClient("openai", openAi);
services.AddKeyedChatClient("anthropic", anthropic);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L16-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-keyed-chat-clients' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddKeyedChatClient` comes with Microsoft.Extensions.AI. Its key is the provider your agents name in `WithModel`, here
`"openai"` and `"anthropic"`, and `openAi` and `anthropic` stand for the `IChatClient` instances your app creates
([A real client](#a-real-client)). Register them however you like, as long as none runs a tool loop of its own
([Wrapping a client](#wrapping-a-client) says why).

With one provider, register one client for every agent instead:

<!-- snippet: chat-client -->
<a id='snippet-chat-client'></a>
```cs
services.AddChatClient(openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L24-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

That client serves every agent whose provider has no client of its own. It also runs an agent that names no model at
all: the run sends no `ModelId`, so the client's own model answers, and a demo or a local test needs nothing else.

For each run, Typeshift takes its client from the first of these that your app registered for it:

1. your `IChatClientProvider<T>` of the agent's [provider id type](#the-provider-id-type), when the run names a
   provider and a model ([Your own provider](#your-own-provider)). It decides alone: when it throws, the run fails,
   and Typeshift tries no client after it.
2. the client registered under the run's provider;
3. the client registered for every agent, which also serves a run that names no provider.

When none is registered, the run throws an `InvalidOperationException` that names what to register.

## Your own provider

Write an `IChatClientProvider<TProviderId>` when a client depends on more than its provider, such as a key per
customer. `TProviderId` is the type your agents name providers by, `string` for `WithModel("openai", ...)`
([The provider id type](#the-provider-id-type)). Its one method gets the provider and the model of every run that
names both, and again when [compaction](./conversations.md#compaction) needs the summary model. Register your class
with `WithChatClientProvider<TProvider>()`, and [A key per customer](#a-key-per-customer) shows one. For a class that
implements no `IChatClientProvider<T>`, the analyzer reports [TSH030](./diagnostics.md#tsh030).

::: info The provider is scoped
Typeshift resolves your provider from each run's scope, so it can use that request's services, such as the current
customer or your `DbContext` ([A key per customer](#a-key-per-customer)). Typeshift never disposes a client you
return. You may return a new instance for every run: the metrics go on across instances, and each call's span names
the backend of the client that answered it ([Spans](./observability.md#spans)).
:::

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

services.AddKeyedChatClient("openai", openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L31-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Typeshift names the model in every request as `ChatOptions.ModelId`, so this one client serves every OpenAI model
  your agents name. The model you pass to `GetChatClient` answers only a run that names no model.
- `OpenAIClient` is in the `OpenAI` namespace. Read the API key from your app's configuration, never from code.

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

services.AddKeyedChatClient("local", local);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L42-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-local-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For other providers, such as Anthropic or Azure OpenAI, register the `IChatClient` of their own package.
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) lists them. A fake client for
tests reads the same requests: the model in `ChatOptions.ModelId`, the agent's instructions in
`ChatOptions.Instructions` and the run's tools in `ChatOptions.Tools`. It calls a tool by answering with a
`FunctionCallContent`, and Typeshift runs the call.

## Providers from configuration

Your app can read its providers from [configuration](https://learn.microsoft.com/dotnet/core/extensions/configuration)
instead of code, so adding a provider or moving one to another endpoint changes only `appsettings.json`. A local
server takes any key, so its entry may hold one, such as `none`:

```json
{
  "Providers": {
    "openai": {},
    "local": { "Endpoint": "http://localhost:1234/v1", "ApiKey": "none" }
  }
}
```

Each entry under `Providers` becomes a keyed singleton `IChatClient`, registered under the entry's name, here for
providers with an OpenAI-compatible API:

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

    services.AddKeyedChatClient(provider.Key, client);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L58-L73' title='Snippet source file'>snippet source</a> | <a href='#snippet-providers-from-configuration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The entry's name, such as `openai`, is the provider your agents name in `WithModel` and your users pick in their
  settings, so a run finds each client by it.
- Keep real keys out of `appsettings.json`. The .NET host reads `Providers:openai:ApiKey` from
  [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in development and from the
  environment variable `Providers__openai__ApiKey` in every environment.
- A provider without a key fails at startup, so a missing secret never surfaces in the middle of a run.

For development, set the key in the app's project folder. `init` writes a `UserSecretsId` into the project file, which
`set` needs:

```bash
dotnet user-secrets init
dotnet user-secrets set "Providers:openai:ApiKey" "<your key>"
```

## A key per customer

When each customer brings their own API key, the client depends on who is asking. Because your provider is scoped, it
can read the current customer and fetch that customer's key for each run:

<!-- snippet: tenant-chat-client-provider -->
<a id='snippet-tenant-chat-client-provider'></a>
```cs
internal sealed class TenantChatClientProvider(ICurrentCustomer customer, ITenantKeys keys)
    : IChatClientProvider<string>
{
    // A client per run, with the customer's current key
    public async ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct)
    {
        if (provider != "openai") throw new InvalidOperationException($"No client for the provider '{provider}'.");

        var key = await keys.GetApiKeyAsync(customer.Id, provider, ct);

        return new OpenAIClient(key)
            .GetChatClient(model)
            .AsIChatClient();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/TenantChatClientProvider.cs#L6-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-tenant-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register it on the chain, in place of a provider for your app's own keys:

<!-- snippet: tenant-chat-client-provider-registration -->
<a id='snippet-tenant-chat-client-provider-registration'></a>
```cs
services.AddTypeshift()
    .WithChatClientProvider<TenantChatClientProvider>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L78-L81' title='Snippet source file'>snippet source</a> | <a href='#snippet-tenant-chat-client-provider-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `ICurrentCustomer` and `ITenantKeys` stand for services of your app: who the request is for, and where you keep the
  keys your customers entered. Both may be scoped, since the provider is.
- A new client per run costs little, because every `OpenAIClient` shares one `HttpClient`, and it always holds the
  customer's current key. A client library whose clients each open their own connections needs a cache instead, kept
  in a singleton and keyed by the API key, so a changed key gets a new client.
- `GetChatClientAsync` throws for any provider but `openai`, so a run that names another fails before it starts
  ([When a provider fails](#when-a-provider-fails)).

## Wrapping a client

You may wrap a provider's client before Typeshift gets it, with the `Use...` calls of Microsoft.Extensions.AI's
[client pipeline](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai), for example to log each request.
`AddKeyedChatClient` registers the client and returns its builder, which resolves the `ILoggerFactory` from your
services:

<!-- snippet: wrapped-chat-client -->
<a id='snippet-wrapped-chat-client'></a>
```cs
var openAi = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();

services.AddKeyedChatClient("openai", openAi)
    .UseLogging();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L86-L93' title='Snippet source file'>snippet source</a> | <a href='#snippet-wrapped-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Wrap what concerns a single request, such as logging or a policy of your own written as a `DelegatingChatClient`. At
the `Trace` level, `UseLogging()` logs every message and answer in full, and those are your customers' data
([Message content](./observability.md#message-content)). Two wrappers repeat what Typeshift already does:

- `UseFunctionInvocation()` runs a second tool loop inside the one Typeshift runs. Its tool calls bypass your
  [tool middleware](./middleware.md) and the run's limits, such as `WithMaxToolIterations`, and a tool that
  [requires approval](./tool-approval.md) never runs there: the model reads that the call was not approved, and your
  app is not asked.
- `UseOpenTelemetry()` records every model call again, on Microsoft.Extensions.AI's own source and meter. Typeshift
  already writes a `chat {model}` span and the metrics for each call ([Spans](./observability.md#spans)), so an app that
  listens to both sees every call twice.

Leave retries to the client library, which knows which of its errors are worth another try. The OpenAI client retries
timeouts, rate limits and server errors three times by default, and its `RetryPolicy` sets how many times.
`ClientRetryPolicy` is in the `System.ClientModel.Primitives` namespace:

<!-- snippet: client-retries -->
<a id='snippet-client-retries'></a>
```cs
var options = new OpenAIClientOptions
{
    RetryPolicy = new ClientRetryPolicy(maxRetries: 5)
};

var openAi = new OpenAIClient(new ApiKeyCredential(apiKey), options)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();

services.AddKeyedChatClient("openai", openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L98-L109' title='Snippet source file'>snippet source</a> | <a href='#snippet-client-retries' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The provider id type

A provider is the value an agent passes to `WithModel`, and that value's type is the agent's provider id type.
`WithModel("openai", "gpt-5-mini")` names a `string`, which suits most apps. You may name providers by a type of
your own instead: an enum suits providers known in code, so the compiler checks each one you name, and a `Guid` suits
providers that are rows in your database. Each agent takes the type from its own `WithModel`, so one app can run an
agent of enum providers beside agents of strings.

Pass a value of your type to `WithModel`, here an enum `Provider` of the Native AOT sample:

<!-- snippet: provider-enum-model -->
<a id='snippet-provider-enum-model'></a>
```cs
WithChatOptions()
    .WithModel(Provider.Scripted, "scripted-model")
    .WithTemperature(0.2f)
    .Configurable(o => o.ModelId)
    .Configurable(o => o.Temperature);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Companies/ResearchAgent.cs#L11-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-enum-model' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Your provider class implements `IChatClientProvider<T>` for that type, so `GetChatClientAsync` takes a `Provider`
instead of a string:

<!-- snippet: provider-enum -->
<a id='snippet-provider-enum'></a>
```cs
internal enum Provider
{
    Scripted
}

// A real host maps each provider to its client here
internal sealed class ScriptedChatClientProvider : IChatClientProvider<Provider>
{
    private static readonly ScriptedChatClient Client = new();

    public ValueTask<IChatClient> GetChatClientAsync(Provider provider, string model, CancellationToken ct) =>
        ValueTask.FromResult<IChatClient>(Client);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Chat/ScriptedChatClientProvider.cs#L6-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-enum' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A run asks the `IChatClientProvider<T>` of its agent's provider type. The runs of this agent reach the class
  above, and those of an agent with string providers reach the class your app registered for strings. One class may
  serve both: `WithChatClientProvider<TProvider>()` registers it as each `IChatClientProvider<T>` it implements.
- Without a provider class for the type, a run falls back as [Registering clients](#registering-clients) lists: to
  a client registered under the provider's value, such as `services.AddKeyedChatClient(Provider.Scripted, client)`,
  then to the client registered for every agent.
- With `Configurable(o => o.ModelId)`, the agent's `Settings` store its provider, so a user's pick is a `Provider`
  too. An agent that makes its model configurable without calling `WithModel` stores providers as strings.
- `Settings` store a provider as a single value, so an agent whose `Settings` store one needs a string, a number, a
  `Guid` or an enum ([TSH029](./diagnostics.md#tsh029)). The generated `Settings` name the type in a file of their
  own, so it may not be nested as `private` or `protected` or be a `file` type, and a public agent's must be public
  ([TSH006](./diagnostics.md#tsh006)).

::: warning A provider class of another type serves nothing
A run asks only the `IChatClientProvider<T>` of its agent's provider type. If your app registers a provider class
for strings and an agent names an enum provider, that agent's runs skip your class without a word and take a client
registered under the provider's value, or the one for every agent, which may be a different model or account. Only
when neither is registered does the run fail, naming the `IChatClientProvider<T>` it needs. Implement the interface
for each provider type your agents use.
:::

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
provider and a model from stored settings or from the run's own settings. A run without them takes the client
registered for every agent, or fails before it starts when there is none
([When a provider fails](#when-a-provider-fails)). Store the pick under a settings id, from a settings page
that offers your providers and their models, for example:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
await support.WithSettingsId(42).ConfigureAsync(settings => settings with
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L13-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L114-L120' title='Snippet source file'>snippet source</a> | <a href='#snippet-summary-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Named | The summary runs on |
|---|---|
| Nothing | The run's provider and model |
| `WithSummaryModel(model)`, or `SummaryModel` in settings | The run's provider, that model |
| `SummaryProvider` and `SummaryModel` in settings | That provider, that model |

::: warning The summary model follows the run's provider
The run's provider is whichever provider the layers resolved for that run, so it changes when a user switches the
agent to another provider. The summary model named in code then goes to that provider too, which may not know it.
Make the summary model configurable wherever users switch providers.
:::

A summary call that fails does not end the run, whether the model is down, its provider throws for it, or the run's
provider does not know it:

1. When the summary named a model or a provider of its own, the run's own provider and model write the summary
   instead.
2. When that call fails too, or the run's own model was the one asked, the run's call goes out without a summary and
   with the oldest tool results cleared. The provider answers it, or rejects it for its length.

Typeshift logs each failed summary call as a warning and records its exception on the run's span. Watch for them,
because every compaction asks a failing summary model again before it falls back.

## When a provider fails

What your code sees depends on whether the run fails before or after it touches the conversation:

| What fails | Your code gets | The run's conversation |
|---|---|---|
| No client serves the run's provider and model, or the agent names no model and no client serves every agent | An `InvalidOperationException` that names the agent and what to register | Unchanged |
| Your provider throws for the run's model, such as for a provider it does not know | Your provider's exception | Unchanged |
| The summary call fails, or your provider throws for the summary model, when compaction asks for it during the run | Nothing: the run goes on ([The summary model](#the-summary-model)) | Goes on, with a summary by the run's own model or without one |
| The model call fails, such as on a wrong key, an unknown model or a rate limit that outlasts the retries | The client library's exception | Ends with an entry that holds an `ErrorContent` |

- Typeshift finds the run's client before it touches the conversation, so the first two fail before anything is
  stored, and before the run's `invoke_agent` span starts. Run the agent again once you fix the
  cause.
- A run that fails later has stored its input already ([What is stored](./conversations.md#what-is-stored)), so a
  retry with the same input puts it into the conversation twice. `ResumeAsync` refuses such a conversation, because
  its last run ended.
- The OpenAI client throws a `ClientResultException`, from `System.ClientModel`, with the HTTP status in `Status`. A
  timeout that outlasts the retries throws an `AggregateException` instead.
- A streaming run throws each of these from the loop over its updates.
- Typeshift never checks a model's name: the provider rejects an unknown one when a run calls it, so a typo saved on
  a settings page fails a later run. Check the pick against the models the provider offers when your settings page
  saves it.

## Related

- [Running an agent](./running-agents.md): the run's services.
- [How a run works](./how-a-run-works.md): the steps of a run.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Builders](./builders.md#chatoptionsbuilder): every chat and run option.
- [Observability](./observability.md): the spans and metrics of each model call.
