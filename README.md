<a id="readme-top"></a>

<div align="center">
  <a href="https://www.nuget.org/packages/Typeshift"><img src="https://img.shields.io/nuget/vpre/Typeshift?style=flat-square&label=nuget" alt="NuGet"></a>
  <a href="https://github.com/weboost-at/Typeshift/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/weboost-at/Typeshift/ci.yml?branch=main&style=flat-square&label=ci" alt="CI"></a>
  <a href="https://weboost-at.github.io/Typeshift/"><img src="https://img.shields.io/badge/docs-guide-blue?style=flat-square" alt="Documentation"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green?style=flat-square" alt="License: MIT"></a>
  <img src="https://img.shields.io/badge/.NET-10-512bd4?style=flat-square" alt=".NET 10">
</div>

---

# Typeshift

**Code-defined agents and toolsets for .NET 10, built on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai).**

Write complex agents like a FluentValidation validator and create tools as static method with support for dependency injection. The Typeshift source-generator further improves agent and tool configuration, it makes tools reflection-free and generates configuration contracts. Simply add Typeshift to your .NET project with `services.AddTypeshift()`.

**[Documentation](https://weboost-at.github.io/Typeshift/)** ·
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) ·
[Diagnostics](https://weboost-at.github.io/Typeshift/guide/diagnostics) ·
[Changelog](CHANGELOG.md)

> [!WARNING]
> Typeshift is in alpha: the API may change between releases. [Status](docs/contributing/status.md) lists what is
> open.

## Why Typeshift?

- **Your XML docs are the tool docs**: the `<summary>` and `<param>` tags you may already write becomes the tools documentation for the model.
- **Invalid agents or tools fail the build**: the analyzer reports a misconfigured tool or agent as a `TSHnnn` diagnostic, it tries its to prevent surprise failures during a runtime invocation.
- **Settings as contracts**: each agent gets a generated `Settings` record holding exactly what may change at runtime, the model included, and nothing else.
- **You own ids and storage**: implement one small store interface, keyed by an id type of your choice.
- **No reflection**: arguments bind and results serialize through generated code, ready for Native AOT.
- **Microsoft.Extensions.AI throughout**: any `IChatClient`, and runs return its `ChatResponse` and updates.

## Feature overview

| Feature | What it gives you |
|---|---|
| [**XML docs are the tool docs**](#xml-docs-are-the-tool-docs) | Tool and parameter descriptions straight from your doc comments |
| [**Configurable tools**](#configurable-tools) | Settings the model cannot spoof, configured in fluent chains |
| [**Composable agents**](#composable-agents) | Agents are composed from toolsets and other agents |
| [**One store**](#one-store-for-settings-and-conversations) | A single persistence interface covers settings and conversations alike |
| [**Conversation tracking**](#conversation-tracking) | Resume, steer and watch runs |
| [**Built-in tools**](#built-in-tools) | To-dos, files, the shell and the web |

| More | |
|---|---|
| [Tool approval](https://weboost-at.github.io/Typeshift/guide/tool-approval) | Pause a run until the host approves a call |
| [Plan and auto modes](https://weboost-at.github.io/Typeshift/guide/modes) | Write a plan or run without interruption for a given task |
| [Middleware](https://weboost-at.github.io/Typeshift/guide/middleware) | Wrap agent-runs or tool calls in middleware |
| [Context providers](https://weboost-at.github.io/Typeshift/guide/context-providers) | Add memory, documents or other data as context to every run |
| [MCP servers](https://weboost-at.github.io/Typeshift/guide/mcp) | Offer an MCP server's tools to the agent |
| [Long-running agents](https://weboost-at.github.io/Typeshift/guide/long-running-agents) | Compaction and crash recovery for hundreds of model calls |
| [Observability](https://weboost-at.github.io/Typeshift/guide/observability) | OpenTelemetry spans and metrics for every run |
| [Native AOT](https://weboost-at.github.io/Typeshift/guide/native-aot) | Publish a host without reflection or trim warnings |

## Quick start

Install the package into every project that declares toolsets or agents:

```bash
dotnet add package Typeshift --prerelease
```

Turn on XML docs, and pick the id type of your settings and conversations once, in `Directory.Build.props`:

```xml
<PropertyGroup>
  <!-- Tools take their descriptions from XML docs -->
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <!-- Ids are Guids unless you name another type; these samples use int -->
  <TypeshiftIdType>int</TypeshiftIdType>
</PropertyGroup>
```

### 1. Define a toolset

A toolset groups multiple tools from a specific domain or task. Each tool must be static method, optionally with XML docs. A tool can optionally take in a settings record, resolved and passed down from Typeshift, excluded from the agent forwarded params :

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
    /// Finds the companies of a city, best matches first.
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

<details>
<summary><b>The settings record</b></summary>

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

</details>

### 2. Define an agent

An agent can be composed of specific tools and other sub-agents best fit for their intended use case. 
It can overwrite tool settings and configure itself options which options may be overwrite-able at runtime:

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>

```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches local companies.");

        WithChatOptions()
            .WithModel("fast-model")
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

### 3. Set up Typeshift

`services.AddTypeshift()` registers every toolset and agent of the project and the projects it references. Register your provider's `IChatClient` under the model's name; `CannedChatClient` is there for demo purposes only, [learn about the client provider](https://weboost-at.github.io/Typeshift/[!todo]):

<!-- snippet: quickstart-registration -->
<a id='snippet-quickstart-registration'></a>

```cs
var services = new ServiceCollection();

services.AddTypeshift()
    .WithStore<InMemoryTypeshiftStore>()
    .WithChatClientProvider<KeyedChatClientProvider>();

services.AddKeyedSingleton<IChatClient>("fast-model", new CannedChatClient());

await using var provider = services.BuildServiceProvider(validateScopes: true);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/Quickstart.cs#L11-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

<details>
<summary><b>The chat client provider</b></summary>

<!-- snippet: chat-client-provider -->
<a id='snippet-chat-client-provider'></a>
```cs
internal sealed class KeyedChatClientProvider(IServiceProvider services) : IChatClientProvider
{
    public ValueTask<IChatClient> GetChatClientAsync(string model, CancellationToken ct) =>
        ValueTask.FromResult(services.GetRequiredKeyedService<IChatClient>(model));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/KeyedChatClientProvider.cs#L6-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-chat-client-provider' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

</details>

### 4. Configure and run an agent

`AgentHost<TAgent>` stores an agent's settings under an id you own:

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

The [getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) tutorial walks through each step.

## Features

### XML docs are the tool docs

A tool's `<summary>` becomes its description and each `<param>` its parameter's description. [Read the detailed docs here](https://weboost-at.github.io/Typeshift/guide/toolsets#names-and-descriptions).

<!-- snippet: toolset -->
<a id='snippet-toolset'></a>

```cs
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        WithDisplayName("Tickets");

        AddTool(SearchTickets)
            .ReadOnly()
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);

        AddTool(GetTicketAsync)
            .WithDescription("Reads one ticket by its number.")
            .ReadOnly();

        AddTool(CloseTicketAsync)
            .RequiresApproval();
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);

    /// <param name="number">The ticket's number.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);

    /// <summary>
    /// Closes a ticket for good.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    /// <param name="reason">Why it is closed, as the customer will read it.</param>
    public static Task CloseTicketAsync(int number, string reason, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().CloseAsync(number, reason, ct);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L5-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

What the model is offered for `search_tickets`:

```json
{
  "name": "search_tickets",
  "description": "Searches the customer's support tickets, newest first.",
  "parameters": {
    "type": "object",
    "properties": {
      "query": { "type": "string", "description": "Words the ticket's title contains." }
    },
    "required": ["query"]
  }
}
```

Note that the settings parameter is excluded from the JSON-schema forwarded to the AI models. This is because Typshift resolves and fills it in when the method is invoked. Just like MEAI provides the `IServiceProvider` and `CancellationToken` parameters for tool methods.

### Configurable tools

A tool's settings are deterministic tool configuration the AI model cannot influence. Examples would be a result limit or a timeout. The tools' configuration chain works like an allow list: `.WithSettings<SearchTicketsSettings>().Configurable(s => s.Limit)` lets `Limit` change at runtime, while
`Timeout` stays what the code says.

<!-- snippet: settings-record -->
<a id='snippet-settings-record'></a>

```cs
internal record SearchTicketsSettings
{
    public int Limit { get; init; } = 10;

    public bool IncludeClosed { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/SearchTicketsSettings.cs#L3-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When configuring an agent, every tool can be configured via the generated `Configure{Tool}()` chain. Which in turn exposes a configuration methods formatted like so: `With{Setting}()` for every property on the tools' settings. 
For the sample above you could overwrite the `Limit` like this: `ConfigureSearchTickets().WithLimit(5)`. 

A settings value can come from [four layers](https://weboost-at.github.io/Typeshift/guide/settings-layering): the setting record's defaults,
the agent, the stored settings and the run. Every call is listed in
[Builders](https://weboost-at.github.io/Typeshift/guide/builders).

### Composable agents

An agent is a composition from multiple other parts, these include: other agents, toolsets, MCP servers, context providers and chat options. [Read the detailed docs here.](https://weboost-at.github.io/Typeshift/guide/defining-agents)

<!-- snippet: agent -->
<a id='snippet-agent'></a>

```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets).Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page").ReadOnly();

        UseContext<CustomerProfileContext>();
        
        UseAgent<ResearchAgent>();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Besides `UseAgent<T>()` Typeshift offers `UseHandoff<T>()` which can be used to hand off the current conversation to another agent. [Multiple agents](https://weboost-at.github.io/Typeshift/guide/multi-agent) compares the two.

As mentioned in the intro, the Typeshift source-generator generates a `Settings` record for each agent. That holds all configurable chat options and the configurable settings of every tool it offers. You may treat that record as a contract for allowed edits to an agent.

### A single store for settings and conversations

Typeshift persists everything through a single interface: `ITypeshiftStore`.
It requires four methods to be implemented, utilizing the id you configured.
Once implemented every conversation and setting can be persisted. 

| Method | Does |
|---|---|
| `LoadAsync` / `SaveAsync` | A document under an id, with a revision that catches concurrent saves |
| `LoadConversationAsync` / `AppendConversationAsync` | A conversation's entries, appended as a run goes |

Without a store, agent settings are resolved from code only. [Read the detailed docs here.](https://weboost-at.github.io/Typeshift/guide/settings-and-stores)

<details>
<summary><b>A store on Entity Framework Core</b></summary>
<!-- snippet: ef-core-store -->
<a id='snippet-ef-core-store'></a>

```cs
internal sealed class EfTypeshiftStore(IDbContextFactory<TypeshiftDb> contexts) : ITypeshiftStore
{
    public async Task<Stored<TData>?> LoadAsync<TData>(int id, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Documents.FindAsync([TData.Kind, id], ct) is { } row
            ? new Stored<TData>(TData.Deserialize(row.Json, row.Version), row.Revision)
            : null;
    }

    public async Task<long> SaveAsync<TData>(int id, TData data, long revision, CancellationToken ct)
        where TData : class, IStorable<TData>
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var row = new DocumentRow
        {
            Kind = TData.Kind,
            Id = id,
            Version = TData.Version,
            Revision = revision + 1,
            Json = data.Serialize()
        };

        var entry = db.Entry(row);
        entry.State = revision == 0 ? EntityState.Added : EntityState.Modified;

        // The update only matches the row while it still has the revision the caller loaded
        if (revision > 0)
            entry.Property(document => document.Revision).OriginalValue = revision;

        // A conflict is another save that won; any other failure is the database's and surfaces as it is
        try
        {
            await db.SaveChangesAsync(ct);
            return row.Revision;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StoreConflictException($"{TData.Kind} {id} changed after revision {revision}.", exception);
        }
        catch (DbUpdateException exception) when (revision == 0)
        {
            if (!await ExistsAsync(TData.Kind, id, ct)) throw;

            throw new StoreConflictException($"{TData.Kind} {id} was saved first by another.", exception);
        }
    }

    private async Task<bool> ExistsAsync(string kind, int id, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);
        return await db.Documents.AnyAsync(row => row.Kind == kind && row.Id == id, ct);
    }

    public async Task<IReadOnlyList<ConversationEntry>> LoadConversationAsync(int conversation, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        var rows = await db.Entries
            .Where(entry => entry.Conversation == conversation)
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(ct);

        return [.. rows.Select(row => ConversationEntry.Deserialize(row.Json, row.Version))];
    }

    public async Task AppendConversationAsync(int conversation, ConversationEntry entry, CancellationToken ct)
    {
        await using var db = await contexts.CreateDbContextAsync(ct);

        db.Entries.Add(new EntryRow
        {
            Conversation = conversation,
            Version = ConversationEntry.Version,
            Json = entry.Serialize()
        });

        await db.SaveChangesAsync(ct);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Stores/EfCore/EfTypeshiftStore.cs#L5-L89' title='Snippet source file'>snippet source</a> | <a href='#snippet-ef-core-store' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

</details>

### Conversation tracking

To track a conversation you must set a conversation id upfront.
It is then used to save  a new or resume an existing conversation. Conversation updates are appended in chunks to reduce context loss on unexpected agent exits. 

<!-- snippet: conversation -->
<a id='snippet-conversation'></a>

```cs
var conversationId = 7;

await support.RunAsync("Where is my refund?", conversationId: conversationId);
await support.RunAsync("When will it arrive?", conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L15-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Use `ResumeAsync` to, as the name suggests, resume a chat without user input. This is useful for recovery purposes. During any chat-client run, the `ConversationTracker` carries it within its list. Using that tracker you can steer sessions with new input, and with `WithRealtimeTracking()` is also streams its updates. [Read the detailed docs here.](https://weboost-at.github.io/Typeshift/guide/conversations)

<!-- snippet: steering -->
<a id='snippet-steering'></a>

```cs
var input = "Send me the invoice too.";

if (!tracker.TrySteer(conversationId, input))
    await support.RunAsync(input, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Conversations.cs#L48-L53' title='Snippet source file'>snippet source</a> | <a href='#snippet-steering' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

### Built-in tools

> [!CAUTION]
> The workspace folder is not a sandbox. Run a host whose agents use the shell in a container.

Typeshift comes with four pre-defined toolsets, these require you, the developer to do some further setup, [refer to the docs on that topic here](https://todo-built-in-tool-setup-docs).


| Name                | Purpose                            |
| ------------------- | ---------------------------------- |
| `TodoToolset`       | Per-session to-do tracking         |
| `FileSystemToolset` | CRUD file system operations        |
| `ShellToolset`      | Execute singular commands          |
| `WebToolset`        | Search and fetch data from the web |

Using these toolsets to their full extent you may set up your own basic coding agent like this:

<!-- snippet: coding-agent -->
<a id='snippet-coding-agent'></a>

```cs
internal partial class CodingAgent : Agent
{
    public CodingAgent()
    {
        WithChatOptions().WithModel("reasoning-model");

        UseToolset<TodoToolset>();
        UseToolset<FileSystemToolset>();
        UseToolset<ShellToolset>();
        UseToolset<WebToolset>().Include(t => t.WebFetch);

        UseAgent<ExploreAgent>().InBackground();

        WithRunOptions()
            .WithMode(AgentMode.Plan)
            .WithMaxToolIterations(500)
            .WithMaxInputTokens(150_000)
            .WithSummaryModel("fast-model")
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations);

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L5-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-coding-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Documentation

- [Get started](https://weboost-at.github.io/Typeshift/guide/getting-started): learn the basics of Typeshift
- [Why code-defined](https://weboost-at.github.io/Typeshift/guide/why-code-defined): the trade-offs, and how Typeshift
  compares with `AIFunctionFactory` and Microsoft Agent Framework
- [How a run works](https://weboost-at.github.io/Typeshift/guide/how-a-run-works): learn about how Typeshift handles your agent-runs
- [Diagnostics](https://weboost-at.github.io/Typeshift/guide/diagnostics): every `TSHnnn` and how to fix it
- [Changelog](CHANGELOG.md)

## Contributing

This repository is set up for agent work, it is recommended to adapt that workflow.
Human PRs are of course appreciated nevertheless. Use the [`AGENTS.md`](AGENTS.md)  and  [contributing guide](https://weboost-at.github.io/Typeshift/contributing/) to gain understanding of the repository structure and how to contribute. The only exception to AI written content are the docs. It may be used to generate an initial draft, but any docs written by any AI must be carefully reviewed and possibly re-written by an human. This project lives off an easy to understand  and well crafted documentation.   

## License

[MIT](LICENSE)

<p align="right"><a href="#readme-top">↑ Back to top</a></p>
