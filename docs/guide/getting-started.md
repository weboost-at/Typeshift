---
title: Getting started
description: Build your first agent in four steps, from installing the package to running it and reading its answer, then see how the pieces fit and how to point a coding assistant at these docs.
---

# Getting started

In four steps you build an agent that looks up the companies of a city, and run it. You write the tool as a static
method and the agent as a small class that offers it. Typeshift turns the method into the tool the model calls, and
the build fails when the agent configures a tool it doesn't offer. [What is Typeshift?](./what-is-typeshift.md)
explains what the library is for, if you'd like that first.

## 1. Install

Add the package to the project that will hold your agent. A console app also needs the dependency injection
container, which builds the service provider in step 4:

```bash
dotnet add package Typeshift --prerelease
dotnet add package Microsoft.Extensions.DependencyInjection
```

Typeshift needs .NET 11. Add `using Typeshift;` to your files, and `using Microsoft.Extensions.DependencyInjection;`
to the `Program.cs` of step 4. A solution of several projects needs the package in more than one of them
([Project setup](./registration.md#project-setup)).

Next to your solution, create a `Directory.Build.props` that turns on XML docs, because Typeshift builds each tool's
description for the model from them. The same file sets the type of the ids you pass to runs. This tutorial uses `int`, which keeps
the ids in its examples short:

```xml
<Project>
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- Ids are Guids unless you name another type. This sample uses int. -->
    <TypeshiftIdType>int</TypeshiftIdType>
  </PropertyGroup>
</Project>
```

## 2. Write a toolset

A toolset is a class that groups related tools, for example the tools of one domain. This one has a single tool,
`FindCompanies`. Put it in a file of its own:

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

    private static readonly Company[] Companies =
    [
        new("Danube Robotics", "Vienna", Active: true),
        new("Prater Motion", "Vienna", Active: true),
        new("Graz Gearworks", "Graz", Active: false)
    ];
}

internal sealed record Company(string Name, string City, bool Active);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/CompanyToolset.cs#L3-L36' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

From the method and its XML docs, Typeshift builds the `find_companies` tool, with a schema that has one parameter,
`city`. You write no JSON schema and no `AIFunctionFactory.Create` call. The class is `partial` because Typeshift adds
members to it, such as the `FindCompaniesOverrides` record that step 4 uses.

The model never sees the `settings` parameter. Typeshift fills it in when it calls the tool, starting from this
record's defaults:

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

`Configurable(s => s.Limit)` in the toolset lets stored settings and the settings of a single run change `Limit`.
Only your code can change `IncludeInactive`, because it isn't marked `Configurable`.

## 3. Write an agent

An agent picks its model and the toolsets it offers in its constructor, and returns its instructions from
`GetInstructions()`:

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

- `WithModel("openai", "gpt-5-mini")` names a provider and the model as that provider calls it. Step 4 registers the
  client for `"openai"`.
- The two `Configurable` calls let stored settings change the instructions and the temperature, but not the model.
- `ConfigureFindCompanies().WithLimit(3)` lowers the limit from the record's 5 to 3 for this agent. Typeshift writes a
  `Configure` method for each tool with settings that the agent offers.

Call one for a tool the agent doesn't offer, such as a ticket search, and the build fails:

```text
ResearchAgent.cs(18,9): error CS0103: The name 'ConfigureSearchTickets' does not exist in the current context
```

## 4. Register and run

`AddTypeshift()` registers every toolset and agent of your project. Chain a store to it, then register the chat
client of the agent's provider under the provider's name:

<!-- snippet: quickstart-clients -->
<a id='snippet-quickstart-clients'></a>
```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>();

services.AddKeyedChatClient("openai", new CannedChatClient());

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L11-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-clients' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`InMemoryTypeshiftStore` keeps settings and conversations in memory and loses them when the app stops. That is fine
for this tutorial and for tests, and [Implementing a store](./implementing-a-store.md) shows how to keep them in your
database.

`CannedChatClient` is a fake from the docs samples, so they run without a network. In your app, register your
provider's client instead. For OpenAI, with the `Microsoft.Extensions.AI.OpenAI` package, that is:

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

`OpenAIClient` is in the `OpenAI` namespace and `AsIChatClient` in `Microsoft.Extensions.AI`. Read `apiKey` from your
app's configuration, for example an environment variable. The key `"openai"` is a name you choose, and it has to
match the provider the agent names in `WithModel`. [A real client](./models.md#a-real-client) shows a local server
and other providers, and [Registering clients](./models.md#registering-clients) one client for every agent.

You configure and run the agent through its host, `AgentHost<ResearchAgent>`. A console app resolves it from a scope,
and a web app takes it as a constructor parameter ([Using an agent](./using-an-agent.md)). Settings are stored under
a settings id that you choose, for example one per customer. This code stores a limit of 2 and a temperature of 0.4
under the id 42:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L22-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ConfigureAsync` loads the agent's generated `ResearchAgent.Settings` record, which holds only what the code marked
`Configurable`, applies your change and saves it. Now run the agent with that settings id, in conversation 1. With
the docs samples' fake client, it prints the line below; a real model words its answer its own way:

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var answer = await research.RunAsync("Vienna", settingsId, conversationId: 1);

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L34-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
research_agent: Found Danube Robotics and Prater Motion.
```

The model called `find_companies` with `Vienna`, and the tool ran with the stored limit of 2, which wins over the
agent's 3 and the record's 5. Vienna has only two active companies, so both fit.

Because the run passed conversation id 1, the store kept the conversation, and a later run with that id continues it
([Conversations](./conversations.md)).

A run can also pass settings of its own, for that run only. With a limit of 1, the tool returns one company:

<!-- snippet: quickstart-run-settings -->
<a id='snippet-quickstart-run-settings'></a>
```cs
var run = new ResearchAgent.Settings
{
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
};

await foreach (var update in research.RunStreamingAsync("Vienna", settingsId, settings: run))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L40-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
Found Danube Robotics.
```

The record, the agent, the stored settings and the run are layers, and the highest one that sets a value wins.
[Settings layering](./settings-and-stores.md#settings-layering) follows this limit through all four.

## What your app implements

Your app implements a few interfaces when it needs the feature that uses them:

| Interface | Needed for | Read |
|---|---|---|
| `IChatClientProvider` | A client that depends on more than its provider, such as a key per customer. | [Your own provider](./models.md#your-own-provider) |
| `ITypeshiftStore`, or `ISettingsStore` and `IConversationStore` | Settings ids and conversation ids. A singleton; `InMemoryTypeshiftStore` serves tests and samples. | [Implementing a store](./implementing-a-store.md) |
| `IContextProvider` | Memory, documents or other data added to every run of an agent. | [Context providers](./middleware.md#context-providers) |
| `IToolApprovalRule`, `IRunMiddleware`, `IToolMiddleware`, `IMcpToolProvider` | Approval rules, middleware and MCP servers. | [The chain](./registration.md#the-chain) |

## Docs for coding assistants

The guide is also published as Markdown for coding assistants such as GitHub Copilot, Claude Code or Cursor. Point
your assistant at one of these files, so it looks up the API these pages describe instead of guessing from what it
learned in training:

| File | Holds |
|---|---|
| [`https://weboost-at.github.io/Typeshift/llms.txt`](https://weboost-at.github.io/Typeshift/llms.txt) | The guide's pages, each with a link and its one-line description |
| [`https://weboost-at.github.io/Typeshift/llms-full.txt`](https://weboost-at.github.io/Typeshift/llms-full.txt) | Every page of the guide in one file |
| A guide page's address with `.md`, such as [`toolsets.md`](https://weboost-at.github.io/Typeshift/guide/toolsets.md) | That page alone |

`llms.txt` suits an assistant that fetches pages as it needs them, because it then reads only the pages of the task
at hand. Add `llms-full.txt` to the context of one that cannot fetch. `llms.txt` follows the
[llms.txt](https://llmstxt.org) format, and all three are rebuilt with the site, so they match the pages you read here.

## Related

- [Toolsets](./toolsets.md): tools, their names, parameters and results in depth.
- [Tool settings](./tool-settings.md): what a settings record may hold and what may change it.
- [Agents](./defining-agents.md): everything an agent's constructor can declare.
- [Using an agent](./using-an-agent.md): the host in a controller, an endpoint or a background job.
- [Models and providers](./models.md): real chat clients, local servers and a client per customer.
- [Settings and stores](./settings-and-stores.md): the settings layers and the store behind them.
- [Running agents](./running-agents.md): structured output and streaming.
- [Conversations](./conversations.md): resuming, steering and watching runs.
- [Glossary](./glossary.md): the words these docs use.
