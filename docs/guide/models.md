---
title: Models and providers
description: Name an agent's model by provider and model, return its client from your IChatClientProvider, choose the provider id type, and pick the summary model.
---

# Models and providers

An agent names its model by a provider and a model, and your `IChatClientProvider` returns the client for that pair.
Typeshift sends each provider its own model id, so your host never builds composite names like `openai/gpt-5-mini`.
A model a user picks is stored like any other setting.

## The chat client provider

You implement one method. Typeshift calls it for every run with the provider and the model the run resolved:

<!-- snippet: chat-client-provider -->
<a id='snippet-chat-client-provider'></a>
```cs
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    // One client per provider serves all its models: Typeshift puts the model into every request
    public ValueTask<IChatClient> GetChatClientAsync(string provider, string model, CancellationToken ct) =>
        ValueTask.FromResult(services.GetRequiredKeyedService<IChatClient>(provider));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/KeyedChatClientProvider.cs#L6-L13' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift puts the model into every request as `ChatOptions.ModelId`, so one client per provider serves all its
models. Register the provider on the chain, and your clients however you like:

<!-- snippet: chat-client-provider-registration -->
<a id='snippet-chat-client-provider-registration'></a>
```cs
services.AddTypeshift()
    .WithChatClientProvider<KeyedChatClientProvider>();

services.AddKeyedSingleton("openai", openAi);
services.AddKeyedSingleton("anthropic", anthropic);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L12-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: info The provider is scoped
Typeshift resolves your provider from each run's scope, so it can use that request's services, such as the current
customer's credentials or your `DbContext`. Typeshift never disposes a client it gets, and it instruments each
provider and model once, whatever instance you return. A client per customer works; one you create per run is yours
to dispose.
:::

A run without a registered provider throws, naming `WithChatClientProvider<T>()`.

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

The agent's `Settings` store a provider as one value, so the type is a string, a number, a `Guid` for providers that
are rows in your database, or an enum for providers known in code. With an enum like the one above, the compiler
checks every provider you name, as in `WithModel(Provider.Scripted, "scripted-model")`. You implement
`IChatClientProvider`, the alias the
generator writes for the type. A provider of another type is [TSH030](./diagnostics.md#tsh030), a type that cannot be
used is [TSH029](./diagnostics.md#tsh029), and a referenced project built with another is
[TSH021](./diagnostics.md#tsh021). A public agent with a configurable model names the type in its `Settings`, so the
type is public too ([TSH006](./diagnostics.md#tsh006)).

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

`Configurable(o => o.ModelId)` lets stored settings or a run pick another, so the agent's
[`Settings`](./generated-members.md#settings) get `Provider` and `Model`. Each
[layers](./settings-and-stores.md#settings-layering) on its own, so a layer that sets only `Model` keeps the
provider below.

Leave out `WithModel` and the choice is the user's. Every run then needs a stored or passed pick, and one without
throws, naming the agent. Store the pick under a settings id, from a settings page that offers your providers and
their models, for example:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L24-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L23-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-summary-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Named | The summary runs on |
|---|---|
| Nothing | The run's provider and model |
| `WithSummaryModel(model)`, or `SummaryModel` in settings | The run's provider, that model |
| `SummaryProvider` and `SummaryModel` in settings | That provider, that model |

::: warning The summary model follows the run's provider
The run's provider is the one the run resolved. When a user switches the agent to another provider, the code's
summary model is asked of that provider too, and a provider that cannot serve it fails the run. Make the summary
model configurable wherever users switch providers.
:::

## Related

- [Running agents](./running-agents.md): the run's services and the steps of a run.
- [Settings and stores](./settings-and-stores.md#settings-layering): where each of a run's values comes from.
- [Registration](./registration.md#the-chain): the chain calls and their lifetimes.
- [Builders](./builders.md#chatoptionsbuilder): every chat and run option.
