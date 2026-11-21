---
title: Models and providers
description: Register clients, name a model and a provider apart, choose both at the call site, run on providers your customers bring, and say what a run does when a model call fails.
---

# Models and providers

An agent names its model, such as `WithModel("gpt-5-mini")`, and may name the provider that serves it, by its key,
such as `WithProvider("openai")`; the code that runs the agent may name others. Typeshift takes the `IChatClient` your
app registered for the run's provider, or the default provider's when the run names none, and sends it the model as
`ChatOptions.ModelId`, so one client serves every model of its provider. A provider nothing is registered for fails
the run: no other client stands in for it.

## Registering clients

With one provider, register one client. It is the default client, which serves every run that names no provider:

<!-- snippet: chat-client -->
<a id='snippet-chat-client'></a>
```cs
services.AddChatClient(openAi);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L37-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An agent then names its model alone, as `WithModel("gpt-5-mini")` does, or no model at all. Without a model the run
sends no `ModelId`, so the client's own model answers, the one you built it with, and a demo or a local test needs
nothing else. `TypeshiftRuntime.Create(client)` registers its client as the default too
([The runtime](./registration.md#the-runtime)).

With several providers, register one client per provider, with the provider's name as its key:

<!-- snippet: keyed-chat-clients -->
<a id='snippet-keyed-chat-clients'></a>
```cs
services.AddKeyedChatClient("openai", openAi);
services.AddKeyedChatClient("anthropic", anthropic);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L18-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-keyed-chat-clients' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddKeyedChatClient` comes with Microsoft.Extensions.AI. Its key is the provider your agents name in
`WithProvider(provider)`, here `"openai"` and `"anthropic"`, and `openAi` and `anthropic` stand for the
`IChatClient` instances your app creates ([A real client](#a-real-client)). Register them however you like, as long
as none runs a tool loop of its own ([Wrapping a client](#wrapping-a-client) says why). An app may register both: a
default client, and clients under the providers some agents name.

Which client a run gets depends on the provider it names: at the call site
([Choosing at the call site](#choosing-at-the-call-site)), in its settings or in its agent's code. Which model it
asks for comes from the same places, and then from the client:

| The run names | Your app registered | The run gets |
|---|---|---|
| No provider | A [default provider](#the-default-provider) | The default provider's client. Naming no provider is how a run means the default. |
| No provider | No default provider | An exception that names the ways to register one or to name a provider |
| A provider's key | A client under that key | That client. It receives the model the run names, and answers with its own when the run names none. |
| A provider's key | Nothing under that key | An exception that lists the keys your app registered clients under |
| A provider document | A connector | The client your connector built for an equal document, made once and kept ([Providers your customers bring](#providers-your-customers-bring)) |
| No model | A client built with a model of its own, as `GetChatClient("gpt-5-mini")` builds one | That model: the client reports it as its `ChatClientMetadata.DefaultModelId` |
| No model | A client without a model of its own | An exception that names the ways to name a model |

The default provider never stands in for a provider a run names. A typo, as in `WithProvider("opnai")`, a
stored `Provider` in another case and a provider your app no longer registers each fail the run, where they would
otherwise send a customer's prompt to another account. The exception is an `InvalidOperationException`, thrown before
the run calls a model or stores an entry in its conversation ([When a provider fails](#when-a-provider-fails)). A run
checks the providers of the agents it may call or hand off to there too, so a sub-agent nothing serves fails the run
at its start, not at its first call.

### The default provider

The default provider serves every run that names no provider. It is the client registered for every agent, or, with
several providers, the one `WithDefaultProvider(key)` names among your keyed clients:

<!-- snippet: default-provider -->
<a id='snippet-default-provider'></a>
```cs
services.AddKeyedChatClient("openai", openAi);
services.AddKeyedChatClient("anthropic", anthropic);

services.AddTypeshift()
    .WithDefaultProvider("openai");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L26-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-default-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- An agent that names no provider then runs on `openai`, and one with `WithProvider("anthropic")` on `anthropic`.
- The key must be one a client is registered under. A key nothing serves fails the runs that take it, and the message
  says that the key is the default provider's and lists the keys that are registered.
- Your app has one default provider. The same key again changes nothing, and a second call with another key throws
  an `InvalidOperationException` that names both, rather than leave the order of two calls to decide.
- An app whose customers each bring their own provider registers no default provider, so a run that forgets to name
  one fails rather than spend your app's own account.

## Choosing at the call site

The code that runs an agent may choose its provider and its model for each run, from what it knows at that moment:
the customer's plan, a choice on a settings page, a test. `WithProvider` and `WithModel` on the agent's host return a
copy of the host, as [`WithSettingsId`](./settings-and-stores.md#ids-of-your-own-types) does:

<!-- snippet: call-site-provider -->
<a id='snippet-call-site-provider'></a>
```cs
var premium = await support
    .WithProvider("anthropic")
    .WithModel("claude-sonnet-5-5")
    .RunAsync("Where is my refund?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L75-L80' title='Snippet source file'>snippet source</a> | <a href='#snippet-call-site-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- What the call site names wins over the agent's code and over every
  [settings layer](./settings-and-stores.md#settings-layering). The model needs no `Configurable(o => o.ModelId)`:
  the allow list guards what stored data may change, and the call site is your app's code.
- The provider serves the whole run: the agent's sub-agents, the agents it hands off to, and the summary of a long
  conversation. A customer who brings a key expects every call of the run on it. The model is the host's agent's
  alone, so each sub-agent keeps its own.
- A copy leaves the host it came from as it was, and a later copy keeps what an earlier one named, so
  `support.WithSettingsId(42).WithProvider("anthropic")` carries both. A copy whose result your code drops changes
  nothing, which the analyzer reports ([TSH041](./diagnostics.md#tsh041)).
- `IAgentHost`, the host of an agent your code does not name, has the same copies
  ([The catalog](./registration.md#the-catalog)).

## Providers your customers bring

When each customer brings their own account, such as their own API key or endpoint, a provider is data your app
owns rather than a client it registers. Derive a record from `ChatProvider` and keep it in your app's storage as you
keep any other entity:

<!-- snippet: customer-provider -->
<a id='snippet-customer-provider'></a>
```cs
// A provider a customer brings: the record your app keeps in its own storage, with the members it needs
internal sealed record CustomerProvider(Guid Id, int Customer) : ChatProvider;
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/CustomerProvider.cs#L3-L6' title='Snippet source file'>snippet source</a> | <a href='#snippet-customer-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `Kind` says which client your connector builds, such as `openai`. `Name` is what spans, entries and messages show
  for the provider. `Endpoint`, `ApiKey` and `Model` are what the client needs, and `Model` answers a run that names
  no model.
- A record compares by its values, the members you add included, so two equal documents are one provider, and a
  rotated key or a new endpoint makes another.
- Typeshift saves, loads and lists no documents. It sees one only when a run is handed it, so where the record lives,
  a table, a file or memory, is your app's choice.

Load the document of the customer the run is for, and hand it to the host:

<!-- snippet: customer-provider-run -->
<a id='snippet-customer-provider-run'></a>
```cs
var account = await providers.LoadAsync(customer.Id, ct);

var reply = await support
    .WithSettingsId(customer.Id)
    .WithProvider(account)
    .RunAsync("Where is my refund?", ct);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/RunningAgents.cs#L87-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-customer-provider-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ICustomerProviders` stands for your app's storage of the documents its customers entered. The document serves the
whole run, its sub-agents and its summary included, as a provider the call site names by key does
([Choosing at the call site](#choosing-at-the-call-site)).

### The connector

One class of yours turns a document into a client, an `IChatProviderConnector`:

<!-- snippet: provider-connector -->
<a id='snippet-provider-connector'></a>
```cs
internal sealed class Connectors : IChatProviderConnector
{
    public ValueTask<IChatClient> ConnectAsync(ChatProvider provider, CancellationToken ct) =>
        ValueTask.FromResult(provider.Kind switch
        {
            "openai" => OpenAi(provider),
            "anthropic" => new AnthropicClient { ApiKey = provider.ApiKey }.AsIChatClient(provider.Model),
            _ => throw new InvalidOperationException($"No connector for the provider kind '{provider.Kind}'.")
        });

    // The document's key and endpoint, and its model as the client's own
    private static IChatClient OpenAi(ChatProvider provider)
    {
        var options = new OpenAIClientOptions();
        if (provider.Endpoint is { } endpoint) options.Endpoint = endpoint;

        var key = provider.ApiKey
            ?? throw new InvalidOperationException($"The provider '{provider.Name}' has no key.");

        return new OpenAIClient(new ApiKeyCredential(key), options)
            .GetChatClient(provider.Model ?? "gpt-5-mini")
            .AsIChatClient();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/Connectors.cs#L8-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-connector' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register it on the chain:

<!-- snippet: provider-connector-registration -->
<a id='snippet-provider-connector-registration'></a>
```cs
services.AddTypeshift()
    .WithProviderConnector<Connectors>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L91-L94' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-connector-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Typeshift asks the connector once for each document it holds no client for, and runs that hand in equal documents
  at the same time share that one call. Every later run with an equal document takes the same client, so build it
  from the document alone: the client outlives the request. The connector is resolved from the run's scope, so it may
  use your services to do its work, such as a vault that holds the keys.
- Typeshift owns the clients your connector returns. One that no run used for
  `TypeshiftOptions.ProviderClientIdleTime`, 10 minutes by default, is disposed when a later run takes a client, and
  disposing the runtime disposes the rest. Clients your app registered itself are never disposed.
- A connector that throws fails the run with its own exception, before the run stores anything. A run handed a
  document while no connector is registered throws an `InvalidOperationException` that names
  `WithProviderConnector<T>()`.
- Your app has one connector, which switches on the document's `Kind`. Registering a second class throws an
  `InvalidOperationException` that names both.
- An agent's code and `WithDefaultProvider` take keys only, and refuse a document with an `ArgumentException`: a
  document belongs to the run your code hands it to.
- Register no default provider when every run should be on a customer's account. A run that forgets `WithProvider`
  then fails, where a default provider would answer it with your app's own key
  ([The default provider](#the-default-provider)).

::: warning Load only the document of the customer the run is for
A document is what a run spends: whoever hands it in runs on that account. Load it from your storage by the customer
the request belongs to, never take one from the request as it is. Keep the keys there in the form you choose, plain,
encrypted by your persistence or as a reference such as `vault:customers/42` that your connector resolves. Typeshift
holds a document in memory while its client lives, writes it nowhere, and leaves `ApiKey` out of the record's
`ToString()`, its spans, its logs, its entries and its messages.
:::

## A real client

Each provider's client library has an adapter to `IChatClient` for Microsoft.Extensions.AI. Add your provider's
package and register one client for the provider, as the snippets below the table show for the first three:

| Provider | Package | The client |
|---|---|---|
| OpenAI | [`Microsoft.Extensions.AI.OpenAI`](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) | `OpenAIClient` |
| Anthropic | [`Anthropic`](https://www.nuget.org/packages/Anthropic) | `AnthropicClient` |
| Ollama, LM Studio | `Microsoft.Extensions.AI.OpenAI` | `OpenAIClient`, at the server's endpoint |
| Azure OpenAI | [`Azure.AI.OpenAI`](https://www.nuget.org/packages/Azure.AI.OpenAI), with `Microsoft.Extensions.AI.OpenAI` | `AzureOpenAIClient`, at your resource's endpoint and adapted as `OpenAIClient` is ([Microsoft's quickstart](https://learn.microsoft.com/dotnet/ai/quickstarts/build-chat-app?pivots=azure-openai)) |

An agent that offers an MCP server the model provider connects needs a client that sends the server to the provider,
and the default clients of OpenAI's and Anthropic's packages drop it
([Servers the model provider connects](./mcp.md#servers-the-model-provider-connects)).

For OpenAI, add the `Microsoft.Extensions.AI.OpenAI` package:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L44-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Typeshift names the model in every request as `ChatOptions.ModelId`, so this one client serves every OpenAI model
  your agents name. The model you pass to `GetChatClient` answers only a run that names no model.
- `OpenAIClient` is in the `OpenAI` namespace. Read the API key from your app's configuration, never from code.

For Anthropic, add the `Anthropic` package, which brings its own adapter:

```bash
dotnet add package Anthropic
```

<!-- snippet: anthropic-client -->
<a id='snippet-anthropic-client'></a>
```cs
var anthropic = new AnthropicClient { ApiKey = apiKey }
    .AsIChatClient("claude-sonnet-5-5");

services.AddKeyedChatClient("anthropic", anthropic);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.Anthropic.cs#L12-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-anthropic-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AnthropicClient` is in the `Anthropic` namespace, and `AsIChatClient` in `Microsoft.Extensions.AI`. As with
OpenAI's client, the run's model goes out in every request, and the model you pass to `AsIChatClient` answers only a
run that names none.

A local server with an OpenAI-compatible API, such as LM Studio or Ollama, works with the OpenAI package. Point the
client at its endpoint, `http://localhost:1234/v1` for LM Studio or `http://localhost:11434/v1` for Ollama, and name
its models in `WithModel`, here as `WithProvider("local").WithModel("qwen3-8b")`. `ApiKeyCredential` is in the
`System.ClientModel` namespace:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L55-L66' title='Snippet source file'>snippet source</a> | <a href='#snippet-local-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Another provider works the same way, through the `IChatClient` its package offers. A fake client for tests reads the
same requests ([Testing an agent](./running-agents.md#testing-an-agent)).

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L71-L86' title='Snippet source file'>snippet source</a> | <a href='#snippet-providers-from-configuration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The entry's name, such as `openai`, is the provider your agents name in `WithProvider` and your users pick in their
  settings, so a run finds each client by it.
- Keep real keys out of `appsettings.json`. Your app's configuration reads `Providers:openai:ApiKey` from
  [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets) in development and from the
  environment variable `Providers__openai__ApiKey` in every environment.
- A provider without a key fails at startup, so a missing secret never surfaces in the middle of a run.

For development, set the key in the app's project folder. `init` writes a `UserSecretsId` into the project file, which
`set` needs:

```bash
dotnet user-secrets init
dotnet user-secrets set "Providers:openai:ApiKey" "<your key>"
```

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L111-L118' title='Snippet source file'>snippet source</a> | <a href='#snippet-wrapped-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Wrap what concerns a single request, such as logging or a policy of your own written as a `DelegatingChatClient`
([Guarding the answer](./middleware.md#guarding-the-answer) shows one that changes what the model says). At
the `Trace` level, `UseLogging()` logs every message and answer in full, and those are your customers' data
([Message content](./observability.md#message-content)). Two wrappers repeat what Typeshift already does:

- `UseFunctionInvocation()` puts a second tool loop inside the one Typeshift runs, where tool calls would bypass
  your [tool middleware](./middleware.md) and the run's limits, such as `WithMaxToolIterations`. A run on such a
  client fails before it calls the model, with an `InvalidOperationException` that says to register the client
  without it. A wrapper of your own that hides the loop from `GetService` goes unnoticed. A tool that
  [requires approval](./tool-approval.md) still never runs there: the model reads that the call was not approved,
  your app is not asked, and Typeshift logs the event `CallWithoutVerdict` ([Logs](./observability.md#logs)).
- `UseOpenTelemetry()` records every model call again, on Microsoft.Extensions.AI's own source and meter. Typeshift
  already writes a `chat {model}` span and the metrics for each call ([Spans](./observability.md#spans)), so an app that
  listens to both sees every call twice.

A wrapper sits in the provider's place, so it receives a tool result that is a JSON object or array as text, the
JSON without white space ([Results](./toolsets.md#results)). The same holds for a client of your own that stands in
for a provider, such as a fake in your tests: where it reads a result, it parses that text.

### Retries

A client library retries on its own, since it knows which of its errors are worth another try. The OpenAI client
retries timeouts, rate limits and server errors three times by default, and its `RetryPolicy` sets how many times.
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L136-L147' title='Snippet source file'>snippet source</a> | <a href='#snippet-client-retries' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A [model policy](#the-model-policy) retries too, and moves a run to another model or provider. Its tries multiply with
the client's, five of the client's for each of the policy's, so set one of the two: the client's retries for a call
that is merely tried again, the policy's rules when a failure should move the run elsewhere.

### Options of a provider's own

Some request options belong to one provider's API, such as whether OpenAI stores the completion, and
`ChatOptions` has no member for them. Microsoft.Extensions.AI's `RawRepresentationFactory` hands the provider's
client an options object of its own to start the request from. For every agent a client serves, set it where you
register the client, with `ConfigureOptions`:

<!-- snippet: client-request-options -->
<a id='snippet-client-request-options'></a>
```cs
var openAi = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();

services.AddKeyedChatClient("openai", openAi)
    .ConfigureOptions(options =>
        options.RawRepresentationFactory = _ => new ChatCompletionOptions { StoredOutputEnabled = false });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L123-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-client-request-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

For one agent, set it in a [run middleware](./middleware.md), which sees the run's options before its model calls:

<!-- snippet: agent-request-options -->
<a id='snippet-agent-request-options'></a>
```cs
internal sealed class UnstoredBilling : IRunMiddleware
{
    public Task InvokeAsync(AgentRun run, Func<Task> next, CancellationToken ct)
    {
        if (run.Agent is BillingAgent)
            run.Options.RawRepresentationFactory = _ => new ChatCompletionOptions { StoredOutputEnabled = false };

        return next();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/UnstoredBilling.cs#L6-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-request-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An agent's `WithChatOptions()` has no call for it: the factory is a function that runs, and an agent's chain only
records values ([How Typeshift reads your code](./how-typeshift-reads-your-code.md)).

## The provider id type

A provider is the key an agent passes to `WithProvider`, and that key's type is the agent's provider id type.
`WithProvider("openai")` names a `string`, which suits most apps. You may name providers by a type of
your own instead: an enum suits providers known in code, so the compiler checks each one you name, and a `Guid` suits
providers that are rows in your database. Each agent takes the type from its own `WithProvider`, so one app can run an
agent of enum providers beside agents of strings.

Pass a value of your type to `WithProvider`, here a member of the Native AOT sample's enum:

<!-- snippet: provider-enum -->
<a id='snippet-provider-enum'></a>
```cs
internal enum Provider
{
    Scripted
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Chat/Providers.cs#L6-L11' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-enum' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<!-- snippet: provider-enum-model -->
<a id='snippet-provider-enum-model'></a>
```cs
WithChatOptions()
    .WithProvider(Provider.Scripted)
    .WithModel("scripted-model")
    .WithTemperature(0.2f)
    .Configurable(o => o.ModelId)
    .Configurable(o => o.Temperature);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Companies/ResearchAgent.cs#L14-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-enum-model' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Register the provider's client under the enum's value, as you register one under a string:

<!-- snippet: provider-enum-client -->
<a id='snippet-provider-enum-client'></a>
```cs
services.AddKeyedChatClient(Provider.Scripted, new ScriptedChatClient());
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Console/Program.cs#L23-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-provider-enum-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Keyed services take keys of any type, so the runs of this agent find the client under `Provider.Scripted`, and
  the runs of an agent with string providers the clients under their strings, in one app. A run of this agent that
  names no provider takes the default provider, whatever its type.
- With `Configurable(o => o.ModelId)`, the agent's `Settings` store its provider, so a user's pick is a `Provider`
  too. An agent that makes its model configurable and names no provider in code, without `WithProvider`, stores
  providers as strings.
- A stored provider that the type no longer has, such as an enum member you renamed or removed, fails the load of
  its settings and every run that applies them, naming the stored text
  ([The stored JSON](./settings-and-stores.md#the-stored-json)). Change the stored values before you ship the rename.
- `Settings` store a provider as a single value, so an agent whose `Settings` store one needs a string, a number, a
  `Guid` or an enum ([TSH029](./diagnostics.md#tsh029)). The generated `Settings` name the type in a file of their
  own, so it may not be nested as `private` or `protected` or be a `file` type, and a public agent's must be public
  ([TSH006](./diagnostics.md#tsh006)).

## Naming a model

`WithModel` takes the model as the provider's API names it, as the
[getting started](./getting-started.md#_3-write-an-agent) agent's `WithModel("gpt-5-mini")` does, and an agent that
names no provider runs on the default client. `WithProvider` names the provider apart from the model, by its key:

<!-- snippet: agent-model -->
<a id='snippet-agent-model'></a>
```cs
WithChatOptions()
    .WithProvider("openai")
    .WithModel("gpt-5-mini")
    .WithTemperature(0.3f)
    .Configurable(o => o.ModelId)
    .Configurable(o => o.Instructions)
    .Configurable(o => o.Temperature);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L15-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-model' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`Configurable(o => o.ModelId)` adds `Provider` and `Model` to the agent's
[`Settings`](./generated-members.md#settings), so stored settings or a run can pick another model. The two values
[layer](./settings-and-stores.md#settings-layering) separately: a layer that sets only `Model` keeps the provider of
the layer below, so set both when you switch providers.

The provider and the model are two calls, so an agent may name a model and leave its provider to the default
client, or name a provider and leave the model to the client. With `Configurable(o => o.ModelId)` a settings layer may
still name a provider for an agent that names none in code, as a string. A layer that names a provider moves
the run off the default client, to the client registered for that provider
([Registering clients](#registering-clients)).

Leave out `WithProvider` and `WithModel` but keep `Configurable(o => o.ModelId)`, and the choice is the user's.
Every run then takes its provider and model from stored settings or from the run's own settings. A run without a provider takes the default
client, or fails before it starts when there is none
([When a provider fails](#when-a-provider-fails)). Store the pick under a settings id, from a settings page
that offers your providers and their models, for example:

<!-- snippet: configure-settings -->
<a id='snippet-configure-settings'></a>
```cs
await support.WithSettingsId(42).UpdateSettingsAsync(settings => settings with
{
    Provider = "anthropic",
    Model = "claude-sonnet-5-5",
    Instructions = "Answer in German.",
    SearchTickets = settings.SearchTickets with { Limit = 20 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L14-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-configure-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A provider in settings picks among the clients your app registered, and so whose key pays for the run. So a
settings page offers the providers the user may pick, rather than taking one from the request as it is
([Settings from a request](./settings-and-stores.md#settings-from-a-request)).

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
    .Configurable(r => r.MaxToolIterations)
    .Configurable(r => r.SummaryModel);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L24-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L152-L158' title='Snippet source file'>snippet source</a> | <a href='#snippet-summary-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Named | The summary runs on |
|---|---|
| Nothing | The run's provider and model |
| `WithSummaryModel(model)`, or `SummaryModel` in settings | The run's provider, that model |
| `SummaryProvider` and `SummaryModel` in settings | That provider, that model |
| `SummaryProvider` alone in settings | That provider, the run's model |

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

Only a call fails that softly. A `SummaryProvider` that no client is registered for fails the run when it starts,
as the run's own provider does, if the run can compact because it has a `MaxInputTokens`
([Registering clients](#registering-clients)).

## When a provider fails

What your code sees depends on whether the run fails before or after it stores its input in the conversation:

| What fails | Your code gets | The run's conversation |
|---|---|---|
| Nothing serves the run: it names a provider your app registered no client for, or no provider while no default provider is registered, or it can compact and its settings name such a summary provider, or an agent it may call or hand off to names such a provider | An `InvalidOperationException` that names the agent, what it named and the keys your app registered clients under | Unchanged |
| No model: no layer names one, and the client that serves the run was built without one | An `InvalidOperationException` that names the ways to name a model | Unchanged |
| Your connector throws for a document, such as one of a kind it does not know | Your connector's exception | Unchanged |
| The summary call fails, when compaction asks for it during the run | Nothing: the run goes on ([The summary model](#the-summary-model)) | Goes on, with a summary by the run's own model or without one |
| The model call fails, such as on a wrong key, an unknown model or a rate limit that outlasts the retries, and no rule of the run's [model policy](#the-model-policy) handles it | The client library's exception, or a `TimeoutException` for the policy's timeout | Ends with an entry that holds an `ErrorContent` |
| The model call fails, and a rule of the run's model policy handles it | Nothing: the call is tried again, or goes to another model or provider | Holds nothing of the failed attempt |

- Typeshift finds the run's client before the run stores its input, so the first three fail with the conversation's
  entries as they were, and before the run's `invoke_agent` span starts. The run has loaded the conversation by
  then, and saved a mode its own settings name ([How a run works](./how-a-run-works.md#the-steps)). Run the agent
  again once you fix the cause.
- A run that fails later has stored its input already ([What is stored](./conversations.md#what-is-stored)), so
  running the agent again with the same input puts it into the conversation twice. `RetryAsync` continues the run
  that failed from its stored steps instead ([Retrying a failed run](./conversations.md#retrying-a-failed-run)).
- The OpenAI client throws a `ClientResultException`, from `System.ClientModel`, with the HTTP status in `Status`. A
  timeout that outlasts the retries throws an `AggregateException` instead.
- A streaming run throws each of these from the loop over its updates.
- Typeshift checks only that a model's name is not blank: `WithModel` and `WithSummaryModel` throw an
  `ArgumentException` for a blank model, and a save refuses blank settings
  ([What a save refuses](./settings-and-stores.md#what-a-save-refuses)). It never checks that the model exists: the
  provider rejects an unknown one when a run calls it, so a typo saved on a settings page fails a later run. Check
  the pick against the models the provider offers when your settings page saves it.

### The model policy

A model policy says what a run does when a model call fails before any of its answer reached your app: try the call
again, move to another model or another provider, or give up waiting. You write it inline, from
`ModelPolicy.Handle(...)`, the way Polly's policies read. An agent declares its own in its run options:

<!-- snippet: model-policy -->
<a id='snippet-model-policy'></a>
```cs
WithRunOptions()
    .WithModelPolicy(ModelPolicy
        .Handle(ModelFailure.Transient).Retry(2, delay: TimeSpan.FromSeconds(1))
        .Or(ModelFailure.Transient | ModelFailure.Rejected).FallbackTo("openai", "gpt-5-mini")
        .Timeout(TimeSpan.FromSeconds(30)));
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/HelpCenterAgent.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-model-policy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A model call starts on the pair of provider and model the run resolved. When it fails, the failure gets a kind
  ([Which failure is which](#which-failure-is-which)), and the first rule, top to bottom, that handles the kind and is
  not used up applies. A failure no rule handles reaches your code as the client threw it.
- `Retry(times, delay, backoff)` tries the call again on the same pair, up to `times` for each model call of the run
  and each pair. It waits `delay` before the first try and multiplies each later wait by `backoff`, 2 unless you set
  it.
- `FallbackToModel(model)`, `FallbackToProvider(key)` and `FallbackTo(key, model)` move the run to another pair. The
  run stays there for the rest of its model calls, and each fallback applies once in a run. A fallback to a provider
  keeps the model the run is on, for one model that two vendors serve; `FallbackTo` names both, for a vendor that
  names the model otherwise. A provider is a key or a [provider document](#providers-your-customers-bring).
- `Timeout(untilFirstUpdate)` counts from the start of an attempt to its first update when the run streams, or to the
  response when it does not. An attempt that waits longer fails as a `Timeout`, which a rule may handle; without one,
  the run throws a `TimeoutException`. An answer that started keeps coming, however long it takes.
- A run checks that a client serves every provider its policy falls back to when it starts, as it checks its own, so
  a fallback nothing serves fails the run before it stores anything, not on the night the first provider is down.
- The policy sits below the steps a conversation stores, so the conversation holds nothing of a failed attempt and
  the user's message once. Once the first update of a streaming answer reached your app, nothing is tried again: the
  failure comes out of the stream, and `RetryAsync` continues the run
  ([Retrying a failed run](./conversations.md#retrying-a-failed-run)).
- Settings cannot store a policy, so `Configurable(r => r.ModelPolicy)` is [TSH012](./diagnostics.md#tsh012).
  Without a policy, a run makes one attempt for each model call, as `ModelPolicy.None` does.

Each retry and each fallback is an event on the run's span, and each attempt has a chat span of its own
([Spans](./observability.md#spans)). The entry of the answer names the provider and the model that gave it.

::: warning The model policy is experimental
`ModelPolicy`, `ModelFailure`, `WithModelPolicy`, `Agent.ModelPolicy` and `ClassifyModelFailure` may still change
before Typeshift 1.0. Code that uses them gets the compiler error `TYPESHIFT001` until it says it knows: add
`#pragma warning disable TYPESHIFT001` to the file, or `TYPESHIFT001` to the project's `NoWarn`.
:::

### A policy at the call site

The code that runs an agent may hand a run another policy, as it hands it a provider
([Choosing at the call site](#choosing-at-the-call-site)). `Agent.ModelPolicy` is the one the agent's code declares,
to start from:

<!-- snippet: call-site-model-policy -->
<a id='snippet-call-site-model-policy'></a>
```cs
var patient = helpCenter.Agent.ModelPolicy.Timeout(TimeSpan.FromMinutes(2));

var answer = await helpCenter
    .WithModelPolicy(patient)
    .RunAsync("How do I change my plan?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/ModelPolicies.cs#L24-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-call-site-model-policy' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The policy of the call site replaces the agent's for the run. Build it from what your app knows when the run
  starts, such as the customer's plan, as in
  `ModelPolicy.Handle(ModelFailure.Auth | ModelFailure.RateLimit).FallbackToProvider("house")` beside the customer's
  own provider.
- It serves the whole run: the agent's sub-agents, the agents it hands off to and the summary of a long conversation.
  A sub-agent goes by its own declared policy only in a run that was handed none.

### Which failure is which

Typeshift references no vendor's client library, so it tells the kind of a failure from .NET's own exceptions:

| The model call threw | Kind |
|---|---|
| An `OperationCanceledException` for a token your app cancelled | None: it reaches your code as it is |
| The policy's timeout; a `TimeoutException`; a cancellation your app did not ask for; status 408 or 504 | `Timeout` |
| Status 429 | `RateLimit` |
| Status 401 or 403 | `Auth` |
| Another status of 500 or more; an `HttpRequestException` without a status; a `SocketException`; an `IOException` | `Unavailable` |
| Status 400, 404, 413 or 422, such as for an unknown model or an input too long | `Rejected` |
| Anything else | `Unknown` |

A status is the `StatusCode` of an `HttpRequestException`. `ModelFailure.Transient` is `RateLimit`, `Timeout` and
`Unavailable` together, and `ModelFailure.Any` is every kind.

A client library throws exceptions of its own, as the OpenAI client throws a `ClientResultException` with the status
in `Status`, and those are `Unknown` until your app says what they are. Tell their kind on the registration chain:

<!-- snippet: classify-model-failure -->
<a id='snippet-classify-model-failure'></a>
```cs
services.AddTypeshift()
    .WithOptions(options => options.ClassifyModelFailure = exception => exception switch
    {
        ClientResultException failed => ModelFailure.FromStatus(failed.Status),
        _ => null
    });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L99-L106' title='Snippet source file'>snippet source</a> | <a href='#snippet-classify-model-failure' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- Your function answers first, for every failure but your app's own cancellation and the policy's timeout. Null
  leaves the exception to Typeshift's table, and `ModelFailure.None` lets it reach your code as it is.
  `ModelFailure.FromStatus(status)` reads a status as the table does.
- An `Unknown` failure that no rule of the run's policy can handle logs the warning `UnknownModelFailure`, once for
  each exception type, which names the type to classify ([Logs](./observability.md#logs)). Otherwise a retry that
  never fires looks like a policy that does not work.
- `ContentFilter` comes from your function alone, since no status tells it. A rule for it sends the content one
  vendor refused to another, so decide whether your app wants that. A refusal that arrives as an ordinary answer is
  no failure at all.

## Related

- [Running an agent](./running-agents.md): the run's services.
- [How a run works](./how-a-run-works.md): the steps of a run.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Builders](./builders.md#chatoptionsbuilder): every chat and run option.
- [Observability](./observability.md): the spans and metrics of each model call.
