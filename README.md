<a id="readme-top"></a>

<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/public/brand/logo-dark.svg">
  <img src="docs/public/brand/logo.svg" alt="Typeshift by weboost" height="56">
</picture>

**Code-defined agents and toolsets for .NET 11, built on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai).**

<a href="https://www.nuget.org/packages/Typeshift"><img src="https://img.shields.io/nuget/vpre/Typeshift?style=flat-square&label=nuget" alt="NuGet"></a>
<a href="https://github.com/weboost-at/Typeshift/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/weboost-at/Typeshift/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
<a href="https://weboost-at.github.io/Typeshift/"><img src="https://img.shields.io/badge/docs-guide-blue?style=flat-square" alt="Documentation"></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License: MIT"></a>
<img src="https://img.shields.io/badge/.NET-11-512bd4?style=flat-square" alt=".NET 11">

**[Documentation](https://weboost-at.github.io/Typeshift/)** ·
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) ·
[Changelog](CHANGELOG.md)

</div>

Build AI agents for .NET with just a few lines of code, and let the compiler catch your mistakes.

> [!WARNING]
> Typeshift is in alpha: the API may change between releases. [Status](docs/contributing/status.md) lists what is
> open.

## Quick start

```bash
dotnet add package Typeshift --prerelease
```

Typeshift needs .NET 11. Its types are in the `Typeshift` namespace, so add `using Typeshift;` to your files.

### 1. Write a toolset

A tool is a static method, and its XML docs tell the model what it does. The model never sees the `settings`
parameter: Typeshift fills it in from your code, stored settings or the run.

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

<details>
<summary><b>The settings record, and what the model sees</b></summary>

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

```json
{
  "name": "find_companies",
  "description": "Finds the companies of a city.",
  "parameters": {
    "type": "object",
    "properties": {
      "city": { "type": "string", "description": "The city the companies are based in." }
    },
    "required": ["city"]
  }
}
```

</details>

### 2. Write an agent

An agent picks its model, its instructions and its tools in its constructor. `Configurable` names what stored
settings may change, and `ConfigureFindCompanies()` sets the tool's limit for this agent:

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

### 3. Run it

Every run needs an `IChatClient`. For OpenAI, add the `Microsoft.Extensions.AI.OpenAI` package and `using OpenAI;` and
`using Microsoft.Extensions.AI;`, and read `apiKey` from your configuration, for example an environment variable. The
agent's `WithModel` picks the model of each run, so the one you pass to `GetChatClient` serves only an agent that
names none:

<!-- snippet: openai-chat-client -->
<a id='snippet-openai-chat-client'></a>
```cs
var client = new OpenAIClient(apiKey)
    .GetChatClient("gpt-5-mini")
    .AsIChatClient();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Models.cs#L127-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-openai-chat-client' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A console app or a script creates the runtime with that client and takes the agent's host from it:

<!-- snippet: typeshift-runtime -->
<a id='snippet-typeshift-runtime'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var research = runtime.Agent<ResearchAgent>();

var answer = await research.RunAsync("Vienna");

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/NoContainer.cs#L11-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-typeshift-runtime' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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

That is all this agent needs: Typeshift keeps settings and conversations in memory until you
[register a store](https://weboost-at.github.io/Typeshift/guide/implementing-a-store#register-your-store).
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) explains every line, and
[what your app implements](https://weboost-at.github.io/Typeshift/guide/getting-started#what-your-app-implements) lists
the interfaces that later features ask for.

## What makes Typeshift different

- **Catch mistakes at compile time.** Configure a tool the agent doesn't offer, and the build fails. The analyzer
  reports the rest as you type, each [diagnostic](https://weboost-at.github.io/Typeshift/guide/diagnostics) with how
  to fix it.
- **Store settings per customer.** Each agent gets a typed `Settings` record that holds only what you mark
  `Configurable`, such as its instructions or a tool's search limit. Keep one per customer under the ids you already
  use, and everything else stays as your code sets it.
- **Stay on Microsoft.Extensions.AI.** Any `IChatClient` serves a model, tools are `AIFunction`s, and a run returns a
  `ChatResponse`, so you keep the providers and types you already use.
- **Publish with Native AOT.** Typeshift writes the tool bindings and the settings serialization when you build, so
  its generated code never reflects over your types. The analyzer names each tool type that still needs
  [JSON metadata](https://weboost-at.github.io/Typeshift/guide/native-aot#tool-parameter-and-result-types).

## What's in the box

| Feature | What it gives you |
|---|---|
| [Tool settings](https://weboost-at.github.io/Typeshift/guide/tool-settings) | Limits and timeouts the model cannot choose, set in code, per customer or per run |
| [Composable agents](https://weboost-at.github.io/Typeshift/guide/defining-agents) | Agents built from toolsets, [sub-agents and handoffs](https://weboost-at.github.io/Typeshift/guide/multi-agent) |
| [Settings and stores](https://weboost-at.github.io/Typeshift/guide/settings-and-stores) | Settings per customer and conversations, kept in memory or in your database |
| [Conversations](https://weboost-at.github.io/Typeshift/guide/conversations) | Resume after a crash, steer a run while it works, watch it live |
| [Tool approval](https://weboost-at.github.io/Typeshift/guide/tool-approval) | Let your app approve a call before it runs |
| [Plan and auto modes](https://weboost-at.github.io/Typeshift/guide/tool-approval#the-three-modes) | Let the agent plan first, or run without interruptions |
| [Middleware](https://weboost-at.github.io/Typeshift/guide/middleware) | Wrap every run or tool call |
| [Context providers](https://weboost-at.github.io/Typeshift/guide/middleware#context-providers) | Add memory, documents or other data to every run |
| [MCP servers](https://weboost-at.github.io/Typeshift/guide/mcp) | Offer an MCP server's tools to an agent |
| [Long-running agents](https://weboost-at.github.io/Typeshift/guide/conversations#compaction) | Compaction and crash recovery for hundreds of model calls |
| [Observability](https://weboost-at.github.io/Typeshift/guide/observability) | OpenTelemetry spans and metrics for every run |

<details>
<summary><b>A coding agent in one class</b>: a workspace toolset, a background sub-agent, plan mode and compaction</summary>

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>
```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseToolset<WorkspaceToolset>();

        UseAgent<ExploreAgent>()
            .InBackground();

        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("claude-haiku-5-5")
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations)
            .Configurable(r => r.SummaryModel);
    }

    protected override string GetInstructions() =>
        """
        Work in the customer's workspace. Read before you change anything, and run the tests after every change.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L37' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

</details>

## Contributing

Contributions are welcome. The repository is set up for coding agents: [`AGENTS.md`](AGENTS.md) maps each task to its
playbook, and the [contributing guide](https://weboost-at.github.io/Typeshift/contributing/) covers building, testing
and the docs. Code may be written with AI. Docs may be drafted with AI, but a person reviews and rewrites them, because
clear docs are what make Typeshift easy to pick up.

## License

[MIT](LICENSE)

<p align="right"><a href="#readme-top">↑ Back to top</a></p>
