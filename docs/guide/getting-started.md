---
title: Getting started
description: Build your first agent in four steps, from installing the package to running it and reading its answer, then see what your app implements and how to point a coding assistant at these docs.
---

# Getting started

In four steps you build an agent that looks up the companies of a city, and run it. You write the tool as a static
method and the agent as a small class that offers it. Typeshift turns the method into the tool the model calls, and
the build fails when the agent configures a tool it doesn't offer. [What is Typeshift?](./what-is-typeshift.md)
explains what the library is for, if you'd like that first.

## 1. Install

Add the package to the project that will hold your agent:

```bash
dotnet add package Typeshift --prerelease
```

Typeshift needs .NET 11. Add `using Typeshift;` to your files. In a solution of several projects, reference the
package in each project that declares toolsets or agents ([Project setup](./registration.md#project-setup)).

The package turns on XML docs in your project, because Typeshift builds each tool's description for the model from
them.

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
        new("Belvedere Optics", "Vienna", Active: true),
        new("Graz Gearworks", "Graz", Active: false)
    ];
}

internal sealed record Company(string Name, string City, bool Active);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/CompanyToolset.cs#L3-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-toolset' title='Start of snippet'>anchor</a></sup>
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

An agent picks its instructions, its model and the toolsets it offers, all in its constructor:

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");
        WithInstructions("Find companies with the find_companies tool and list them.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.2f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/ResearchAgent.cs#L3-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `WithModel("openai", "gpt-5-mini")` names a provider and the model as that provider calls it. Step 4 creates the
  client that serves it.
- The two `Configurable` calls let stored settings change the instructions and the temperature, but not the model.
- `ConfigureFindCompanies().WithLimit(3)` lowers the limit from the record's 5 to 3 for this agent. Typeshift writes a
  `Configure` method for each tool with settings that the agent offers.

Call one for a tool the agent doesn't offer, such as a ticket search, and the build fails:

```text
ResearchAgent.cs(18,9): error CS0103: The name 'ConfigureSearchTickets' does not exist in the current context
```

## 4. Run it

Every run needs an `IChatClient`, the interface Microsoft.Extensions.AI has for a model. For OpenAI, add the
`Microsoft.Extensions.AI.OpenAI` package and create its client:

<!-- snippet: openai-chat-client -->
<a id='snippet-openai-chat-client'></a>
```cs
var client = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L127-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`OpenAIClient` is in the `OpenAI` namespace and `AsIChatClient` in `Microsoft.Extensions.AI`. Read `apiKey` from your
app's configuration, for example an environment variable. Each run asks for the model its agent names in `WithModel`,
so the model you pass to `GetChatClient` answers only an agent that names none.
[A real client](./models.md#a-real-client) shows a local server and other providers.

Create the runtime with that client, take the agent's host from it and run the agent:

<!-- snippet: quickstart-runtime -->
<a id='snippet-quickstart-runtime'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var research = runtime.Agent<ResearchAgent>();

var first = await research.RunAsync("Vienna");

Console.WriteLine($"{research.Agent.Name}: {first.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L9-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-runtime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
research_agent: Found Danube Robotics, Prater Motion and Belvedere Optics.
```

- `TypeshiftRuntime.Create(client)` sets up Typeshift in an app without a dependency injection container, such as a
  console app or a script, and its one client serves every run ([The runtime](./registration.md#the-runtime)).
- `runtime.Agent<ResearchAgent>()` returns the agent's host, `AgentHost<ResearchAgent>`. You configure and run the
  agent through it.
- The model called `find_companies` with `Vienna`, and the tool ran with the agent's limit of 3, so the answer lists
  the three active companies of Vienna.

The answers on this page come from the canned client of the
[docs samples](https://github.com/weboost-at/Typeshift/tree/main/samples/Typeshift.Samples.Docs), which lists what the
tool returned. A real model words its answer its own way.

Settings are stored under a settings id that you choose, for example one per customer.
`research.WithSettingsId(settingsId)` returns a copy of the host whose runs and settings members use the settings
under that id, so this code stores a limit of 2 and a temperature of 0.4 under the id 42:

<!-- snippet: quickstart-settings -->
<a id='snippet-quickstart-settings'></a>
```cs
var settingsId = 42;

await research.WithSettingsId(settingsId).ConfigureAsync(settings => settings with
{
    Temperature = 0.4f,
    FindCompanies = settings.FindCompanies with { Limit = 2 }
});
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L18-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`ConfigureAsync` loads the agent's generated `ResearchAgent.Settings` record, which holds only what the code marked
`Configurable`, applies your change and saves it. Typeshift keeps settings and conversations in memory until you
[register a store](./implementing-a-store.md#register-your-store).

Now run the agent with that settings id, and add `WithConversationId(1)` so the run takes place in conversation 1:

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var answer = await research.WithSettingsId(settingsId).WithConversationId(1).RunAsync("Vienna");

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L28-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
research_agent: Found Danube Robotics and Prater Motion.
```

This time the tool ran with the stored limit of 2, which wins over the agent's 3 and the record's 5, so the answer
lists two companies.

Because the run had conversation id 1, Typeshift kept the conversation, and a later run with that id continues it
([Conversations](./conversations.md)).

A run can also pass settings of its own, for that run only. With a limit of 1, the tool returns one company:

<!-- snippet: quickstart-run-settings -->
<a id='snippet-quickstart-run-settings'></a>
```cs
var run = new ResearchAgent.Settings
{
    FindCompanies = new CompanyToolset.FindCompaniesOverrides { Limit = 1 }
};

await foreach (var update in research.WithSettingsId(settingsId).RunStreamingAsync("Vienna", settings: run))
    Console.Write(update.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L34-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
Found Danube Robotics.
```

The record, the agent, the stored settings and the run are layers, and the highest one that sets a value wins.
[Settings layering](./settings-and-stores.md#settings-layering) follows this limit through all four.

In an app with a container, such as ASP.NET Core, `AddTypeshift()` registers every toolset and agent, and you inject
the agent's host where you run it:

<!-- snippet: minimal-api -->
<a id='snippet-minimal-api'></a>
```cs
builder.Services.AddTypeshift();
builder.Services.AddChatClient(client);

var app = builder.Build();

app.MapGet("/research/{city}", async (string city, AgentHost<ResearchAgent> research) =>
    (await research.RunAsync(city)).Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MinimalApi.cs#L21-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-minimal-api' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`builder` is the `WebApplicationBuilder` of your `Program.cs`. The endpoint takes the host as a parameter, and
[Using an agent](./using-an-agent.md) shows it in a class of your own and in a background job.

## What your app implements

Your app implements a few interfaces when it needs the feature that uses them:

| Interface | Needed for | Read |
|---|---|---|
| `IChatClientProvider<T>` | A client that depends on more than its provider, such as a key per customer. | [Your own provider](./models.md#your-own-provider) |
| `ITypeshiftStore<TId>`, or `ISettingsStore<TId>` and `IConversationStore<TId>` | Settings and conversations under ids of your type `TId`; `IAgentSettingsStore` for each agent's own settings. A singleton; `InMemoryTypeshiftStore<TId>` serves tests and samples. | [Implementing a store](./implementing-a-store.md) |
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

- [The quickstart's code](https://github.com/weboost-at/Typeshift/tree/main/samples/Typeshift.Samples.Docs/Quickstart):
  the toolset, the settings record, the agent and the runs of this page, as the docs samples build and run them.
- [Toolsets](./toolsets.md): tools, their names, parameters and results in depth.
- [Tool settings](./tool-settings.md): what a settings record may hold and what may change it.
- [Agents](./defining-agents.md): everything an agent's constructor can declare.
- [Using an agent](./using-an-agent.md): the host in a controller, an endpoint or a background job.
- [Models and providers](./models.md): real chat clients, local servers and a client per customer.
- [Settings and stores](./settings-and-stores.md): the settings layers and the store behind them.
- [Running an agent](./running-agents.md): structured output and streaming.
- [Conversations](./conversations.md): resuming, steering and watching runs.
- [Glossary](./glossary.md): the words these docs use.
