<a id="readme-top"></a>

<div align="center">

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/public/brand/logo-dark.svg">
  <img src="docs/public/brand/logo.svg" alt="Typeshift by weboost" height="56">
</picture>

**Code-defined agents and toolsets for .NET 10, built on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai).**

<a href="https://www.nuget.org/packages/Typeshift"><img src="https://img.shields.io/nuget/vpre/Typeshift?style=flat-square&label=nuget" alt="NuGet"></a>
<a href="https://github.com/weboost-at/Typeshift/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/weboost-at/Typeshift/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
<a href="https://weboost-at.github.io/Typeshift/"><img src="https://img.shields.io/badge/docs-guide-blue?style=flat-square" alt="Documentation"></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License: MIT"></a>
<img src="https://img.shields.io/badge/.NET-10-512bd4?style=flat-square" alt=".NET 10">

**[Documentation](https://weboost-at.github.io/Typeshift/)** ·
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) ·
[Changelog](CHANGELOG.md)

</div>

Build AI agents for .NET with just a few lines of code, and let the compiler catch your mistakes.

> [!WARNING]
> Typeshift is in alpha: the API may change between releases. [Status](docs/contributing/status.md) lists what is
> open.

## Why Typeshift?

- **Design your toolsets.** `AddTool(FindCompanies)` turns a static method into a tool, and its XML docs become what
  the model reads. Group your tools by feature, domain or permission level, as you like.
- **Create an agent.** A few chained calls in its constructor pick the model, the tools and their settings.
- **Catch mistakes at compile time.** Configuring a tool the agent does not offer does not compile, and the analyzer
  reports the rest as [diagnostics](https://weboost-at.github.io/Typeshift/guide/diagnostics).
- **Store settings per customer.** Each agent gets a generated `Settings` record of what you mark configurable. Store
  it in your database, under the id type you already use.
- **Stay on Microsoft.Extensions.AI.** Any `IChatClient` serves a model, nothing uses reflection, and your app
  publishes with Native AOT.

## Quick start

```bash
dotnet add package Typeshift --prerelease
```

The types are in the `Typeshift` namespace, so add `using Typeshift;` to your files. Turn on XML docs and pick your id
type once, in a `Directory.Build.props` next to your solution:

```xml
<Project>
  <PropertyGroup>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- Ids are Guids unless you name another type -->
    <TypeshiftIdType>int</TypeshiftIdType>
  </PropertyGroup>
</Project>
```

### 1. Write a toolset

A tool is a static method. Its XML docs describe it to the model, and its settings stay out of the model's reach:

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

<details>
<summary><b>What the model sees</b></summary>

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

The constructor picks the model, the tools and their settings. `Configurable` names what may change at runtime:

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

### 3. Run it

`AddTypeshift()` registers every toolset and agent. You bring a chat client provider, which returns the `IChatClient`
of a provider's model, and a store for settings and conversations:

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

`CannedChatClient` is a fake that lets the samples run without a network. In your app, register a real client in its
place, such as [OpenAI's](https://weboost-at.github.io/Typeshift/guide/models#a-real-client).

<details>
<summary><b>The chat client provider</b></summary>

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

</details>

Store settings under an id you own, then run the agent:

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

<!-- snippet: quickstart-run -->
<a id='snippet-quickstart-run'></a>
```cs
var answer = await research.RunAsync("Vienna", settingsId, conversationId: 1);

Console.WriteLine($"{research.Agent.Name}: {answer.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L35-L39' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The [getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) guide explains every line, and
lists [what your app implements](https://weboost-at.github.io/Typeshift/guide/getting-started#what-your-app-implements).

## What's in the box

| Feature | What it gives you |
|---|---|
| [Tool settings](https://weboost-at.github.io/Typeshift/guide/tool-settings) | Limits and timeouts the model cannot choose, set in code, per customer or per run |
| [Composable agents](https://weboost-at.github.io/Typeshift/guide/defining-agents) | Agents built from toolsets, [sub-agents and handoffs](https://weboost-at.github.io/Typeshift/guide/multi-agent) |
| [One store](https://weboost-at.github.io/Typeshift/guide/settings-and-stores) | Settings and conversations in your database, behind four methods |
| [Conversations](https://weboost-at.github.io/Typeshift/guide/conversations) | Resume after a crash, steer a run while it works, watch it live |
| [Built-in toolsets](https://weboost-at.github.io/Typeshift/guide/built-in-toolsets) | To-dos, files, the shell and the web |
| [Tool approval](https://weboost-at.github.io/Typeshift/guide/tool-approval) | Let your app approve a call before it runs |
| [Plan and auto modes](https://weboost-at.github.io/Typeshift/guide/tool-approval#the-three-modes) | Let the agent plan first, or run without interruptions |
| [Middleware](https://weboost-at.github.io/Typeshift/guide/middleware) | Wrap every run or tool call |
| [Context providers](https://weboost-at.github.io/Typeshift/guide/middleware#context-providers) | Add memory, documents or other data to every run |
| [MCP servers](https://weboost-at.github.io/Typeshift/guide/mcp) | Offer an MCP server's tools to an agent |
| [Long-running agents](https://weboost-at.github.io/Typeshift/guide/conversations#compaction) | Compaction and crash recovery for hundreds of model calls |
| [Observability](https://weboost-at.github.io/Typeshift/guide/production#observability) | OpenTelemetry spans and metrics for every run |
| [Native AOT](https://weboost-at.github.io/Typeshift/guide/production#native-aot) | Publish your app without reflection or trim warnings |

### A coding agent in one class

The built-in toolsets, a background sub-agent, plan mode and compaction, together:

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>
```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();
        UseToolset<WebToolset>()
            .Include(t => t.WebFetch);

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

        ConfigureRunCommand()
            .WithShell("/bin/bash")
            .WithTimeout(TimeSpan.FromMinutes(2));
    }

    protected override string GetInstructions() =>
        """
        Work in the customer's workspace. Plan with write_todos, read before you edit, and run the tests after
        every change.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

> [!CAUTION]
> The workspace folder is not a sandbox. Run an app whose agents use the shell in a container.

## Contributing

Contributions are welcome. The repository is set up for coding agents: [`AGENTS.md`](AGENTS.md) maps each task to its
playbook, and the [contributing guide](https://weboost-at.github.io/Typeshift/contributing/) covers building, testing
and the docs. Code may be written with AI. Docs may be drafted with AI, but a person reviews and rewrites them, because
clear docs are what make Typeshift easy to pick up.

## License

[MIT](LICENSE)

<p align="right"><a href="#readme-top">↑ Back to top</a></p>
