---
title: Getting started
description: Build and run a first agent in four steps, then see how the pieces fit and what your app implements.
---

# Getting started

Typeshift lets you declare tools and agents in a few lines of C#. In four steps, this page builds an agent that finds
the companies of a city.

## 1. Install

Typeshift needs .NET 10. Add the package to every project that declares toolsets or agents
([TSH020](./diagnostics.md#tsh020)) or calls `AddTypeshift()`:

```bash
dotnet add package Typeshift --prerelease
```

The types on this page are in the `Typeshift` namespace, which the package does not import for you, so add
`using Typeshift;` to your files. Step 4 also needs `using Microsoft.Extensions.AI;` and
`using Microsoft.Extensions.DependencyInjection;`, and in a console app the `Microsoft.Extensions.DependencyInjection`
package, which builds the service provider.

Turn on XML docs in `Directory.Build.props`, since tools take their descriptions from them. The same file sets
`TypeshiftIdType`, the type of the settings ids and conversation ids you pass to runs in step 4:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Ids are Guids unless you name another type. This sample uses int. -->
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```

## 2. Write a toolset

A toolset declares tools in its constructor. Each tool is a static method:

<!-- snippet: quickstart-toolset -->
<a id='snippet-quickstart-toolset'></a>
```cs
internal partial class CompanyToolset : Toolset
{
    public CompanyToolset()
    {
        WithDisplayName("Companies");

        AddTool(FindCompanies)
            .WithSettings<FindCompaniesSettings>()
            .Configurable(s => s.Limit);
    }

    /// <summary>
    /// Finds the companies of a city.
    /// </summary>
    /// <param name="city">The city the companies are based in.</param>
    public static IReadOnlyList<Company> FindCompanies(string city, FindCompaniesSettings settings) =>
    [
        .. Companies
            .Where(company => company.City.Equals(city, StringComparison.OrdinalIgnoreCase))
            .Where(company => company.Active || settings.IncludeInactive)
            .Take(settings.Limit)
    ];
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/CompanyToolset.cs#L3-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The model calls the tool as `find_companies` and reads its XML docs.
- The model never sees the `FindCompaniesSettings` parameter. Typeshift fills it with the tool's settings when it
  calls the tool.
- `Configurable(s => s.Limit)` lets stored settings and runs change `Limit`, while `IncludeInactive` stays as your
  code sets it.

The settings record sets the defaults:

<!-- snippet: quickstart-settings-record -->
<a id='snippet-quickstart-settings-record'></a>
```cs
internal record FindCompaniesSettings
{
    public int Limit { get; init; } = 5;

    public bool IncludeInactive { get; init; }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/FindCompaniesSettings.cs#L3-L10' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## 3. Write an agent

An agent picks a model, its instructions and its tools:

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.2f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() =>
        """
        Find companies with the find_companies tool and list them.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/ResearchAgent.cs#L3-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `Configurable` lets stored settings and runs change the instructions and the temperature. The model stays as the
  code sets it.
- The generator writes `ConfigureFindCompanies()` because the agent uses `CompanyToolset`, and `WithLimit(3)` sets
  this agent's limit over the record's 5. Configuring a tool the agent does not offer fails the build.

## 4. Run it

`AddTypeshift()` registers every toolset and agent. Chain a store, which keeps settings and conversations, and a chat
client provider, which returns the `IChatClient` that serves each model:

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>();

services.AddKeyedSingleton<IChatClient>("openai", new CannedChatClient());

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L11-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`CannedChatClient` is a fake client from the docs samples that stands in for a real model. Register your provider's
`IChatClient` under `"openai"` instead, as [Models and providers](./models.md) shows. `InMemoryTypeshiftStore` comes
with Typeshift and stands in for your database.

`KeyedChatClientProvider` is your `IChatClientProvider`. For each run, Typeshift asks it for the client of the
provider and model the agent names, here `"openai"` and `"gpt-5-mini"`, and it returns the `IChatClient` registered
under the provider's key:

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

A settings id is an id you choose, for example one per customer. The store keeps the agent's settings under it, and
every run that passes the id uses them. `ConfigureAsync` loads the agent's generated `ResearchAgent.Settings` record,
which holds only what the code marked `Configurable`, applies your change and saves it:

<!-- snippet: quickstart-settings -->
<a id='snippet-quickstart-settings'></a>
```cs
await using var scope = provider.CreateAsyncScope();
var research = scope.ServiceProvider.GetRequiredService<AgentHost<ResearchAgent>>();
var settingsId = 42;

await research.ConfigureAsync(settingsId, settings => settings with
{
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L23-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Every run with settings id 42 now finds at most 2 companies. The conversation id is optional. With it, Typeshift
stores the run under conversation 1, and a later run with the same id continues the conversation:

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var answer = await research.RunAsync("Vienna", settingsId, conversationId: 1);

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L35-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The limit passes through three layers: 5 in the record, 3 in the agent and 2 in the stored settings. The highest
layer that sets a value wins, as [Settings layering](./settings-and-stores.md#settings-layering) explains.

## How the pieces fit

```mermaid
flowchart LR
  subgraph compile [Compile time]
    toolset[Toolset] --> generator[Source generator]
    agent[Agent] --> generator
    generator --> functions[An AIFunction per tool]
    generator --> settings[A Settings record per agent]
    generator --> registration["AddTypeshift()"]
  end
  subgraph runtime [Runtime]
    host["AgentHost&lt;TAgent&gt;"] --> client[The model's IChatClient]
    host --> store[Your ITypeshiftStore]
  end
  registration --> host
```

`AddTypeshift()` registers an `AgentHost<TAgent>` per agent. The host resolves the settings, calls the model, runs the
tools and stores the conversation.

| Piece | What it is | Read |
|---|---|---|
| Toolset | A `partial` class deriving from `Toolset` that declares the tools of one domain | [Toolsets](./toolsets.md) |
| Tool settings | A record of values a tool needs that the model must not choose | [Tool settings](./tool-settings.md) |
| Agent | A `partial` class deriving from `Agent`, with chat options, instructions and tools | [Agents](./defining-agents.md) |
| Agent host | `AgentHost<TAgent>`, which runs one agent in the scope it was resolved from | [Running agents](./running-agents.md) |
| Store | Your `ITypeshiftStore`, which keeps settings and conversations | [Settings and stores](./settings-and-stores.md) |

The [glossary](./glossary.md) defines every other term.

## What your app implements

Your app implements a few interfaces. Every run needs a chat client provider, and the others come with the feature
that uses them:

| Interface | Needed for | Read |
|---|---|---|
| `IChatClientProvider` | Every run. It returns the `IChatClient` for a provider and a model. | [Models and providers](./models.md) |
| `ITypeshiftStore` | Settings ids and conversation ids. `InMemoryTypeshiftStore` serves tests and samples. | [Settings and stores](./settings-and-stores.md) |
| `IFileSystemRoot`, `IWebSearch`, `IWebFetch` | The built-in file, shell and web tools. | [Built-in toolsets](./built-in-toolsets.md#what-each-needs) |
| `IContextProvider` | Memory, documents or other data added to every run of an agent. | [Context providers](./middleware.md#context-providers) |
| `IToolApprovalRule`, `IRunMiddleware`, `IToolMiddleware`, `IMcpToolProvider` | Approval rules, middleware and MCP servers. | [The chain](./registration.md#the-chain) |

## Related

- [Toolsets](./toolsets.md): tools, their names, parameters and results in depth.
- [Tool settings](./tool-settings.md): what a settings record may hold and what may change it.
- [Agents](./defining-agents.md): everything an agent's constructor can declare.
- [Running agents](./running-agents.md): real chat clients, structured output and streaming.
- [Conversations](./conversations.md): resuming, steering and watching runs.
- [Glossary](./glossary.md): the words these docs use.
