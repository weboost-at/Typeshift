---
title: Agents
description: Declare an agent in its constructor, with its chat options, instructions, tools and their settings.
---

# Agents

An agent bundles what a model needs for one job: chat options, instructions and tools. You declare it in its
constructor, and the generator gives it a `Settings` record of what may change at runtime.

## Declaring an agent

An agent is a `partial` class deriving from `Agent`. This one answers customers from their tickets and the help
center:

<!-- snippet: agent -->
<a id='snippet-agent'></a>
```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>()
            .Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page")
            .ReadOnly();

        UseContext<CustomerProfileContext>();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The tools and their settings have sections of their own below. The other calls declare the rest:

| Call | Declares |
|---|---|
| `WithName(...)` | The agent's name. Messages the agent writes carry it as their author, and other agents call this one by it. Without `WithName`, `SupportAgent` is named `support_agent`. |
| `WithDescription(...)` | What the agent does. The model of an agent that calls this one reads it. |
| `WithChatOptions()` | The options every run starts from: `WithModel`, `WithTemperature`, `WithMaxOutputTokens` and a `With...` per other option. `WithTools` adds tools that belong to no toolset, such as a hosted web search. |
| `UseMcpServer(...)` | The tools of an [MCP server](./mcp.md). `ReadOnly()` lets plan mode offer them. |
| `UseContext<T>()` | A [context provider](./middleware.md#context-providers), whose instructions, messages and tools join every run. |
| `UseAgent<T>()`, `UseHandoff<T>()` | Another agent as a tool, or one the model can hand the conversation to. See [Multiple agents](./multi-agent.md). |
| `GetInstructions()` | The instructions, as an override. It runs whenever a run's options are resolved, so the text may be computed. |

Because the generator reads the constructor rather than running it, `WithName` and `WithDescription` take constants
([TSH002](./diagnostics.md#tsh002)), and every declaration sits directly in the constructor as one chain
([TSH014](./diagnostics.md#tsh014), [TSH015](./diagnostics.md#tsh015)). An agent derives from `Agent` directly, never
from a base agent of yours ([TSH017](./diagnostics.md#tsh017)). To share between agents, put values in constants and
code in static methods, and keep every declaration in each agent's constructor.

Because an agent is declared in code, you cannot define one at runtime, and stored settings change only what its code
marks configurable.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only the tools you pick, in the order you
pick them, or `Exclude` to offer all but those:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>()
    .Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The lambda picks from a list the generator writes for each toolset, with one property per tool, named after the
tool: `search_tickets` is `t.SearchTickets`, and `close_ticket`, from `CloseTicketAsync`, is `t.CloseTicket`. A tool
of another toolset is not on that list, so picking it does not compile. Use each toolset once and pick each tool once
([TSH025](./diagnostics.md#tsh025)). A toolset from another project needs the Typeshift package in that project
([TSH019](./diagnostics.md#tsh019)).

## Add a tool of the agent's own

A tool that only one agent needs can live in the agent itself, without a toolset of its own. Declare it with `AddTool`
in the agent's constructor, backed by a static method of the agent:

<!-- snippet: agent-own-tool -->
<a id='snippet-agent-own-tool'></a>
```cs
internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Settles charges and invoices.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        AddTool(GetToday)
            .ReadOnly();
    }

    /// <summary>
    /// Returns today's date, to tell whether an invoice is overdue.
    /// </summary>
    private static DateOnly GetToday(IServiceProvider services)
    {
        var time = services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(time.GetLocalNow().DateTime);
    }

    protected override string GetInstructions() => "Settle the customer's charges and invoices.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/BillingAgent.cs#L3-L28' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-own-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The tool takes the same chain as a toolset's tool, such as `ReadOnly()` here or `RequiresApproval()`, and follows
  the same rules for its [name and description](./toolsets.md#names-and-descriptions),
  [parameters](./toolsets.md#parameters) and [results](./toolsets.md#results). So `GetToday` becomes `get_today`, its
  `<summary>` describes it to the model, and the `IServiceProvider` it takes is the run's, which the model never sees.
- The method must be static and may be private, since only the agent's generated code calls it.
- The agent offers its own tools after the tools of its toolsets, in the order it adds them.
- A tool with settings declares them with `WithSettings<T>()` and takes the record as a parameter
  ([Tool settings](./tool-settings.md)). It then gets a `Configure{Tool}()`, and its configurable members join the
  agent's `Settings`, as a toolset's tool does.
- Its name must differ from the tools of the toolsets the agent uses ([TSH025](./diagnostics.md#tsh025)) and from the
  tools of the agents it hands a conversation to or receives one from, directly or through other agents, and of its
  sub-agents and their callers ([TSH007](./diagnostics.md#tsh007)). Other agents may have a tool of the same name.

No other agent can offer this agent's tools, so move the tool into a toolset once a second agent needs it. `Include`
and `Exclude` pick only from toolsets, so the agent always offers its own tools.

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that stored settings and runs may change, whether the code sets it
or not. The model is an option like any other. Without `Configurable(o => o.ModelId)`, stored settings and runs
cannot change it, and the agent's `Settings` has no `Provider` or `Model` for a settings form to offer.

[Builders](./builders.md#chatoptionsbuilder) lists which options may be configurable. The rest belong to the run
rather than to its settings, and the analyzer reports a `Configurable` call that names one of them
([TSH012](./diagnostics.md#tsh012)).

## Run options

`WithRunOptions()` sets how a run's tool loop goes, and `Configurable` marks the options that stored settings and
runs may change:

<!-- snippet: run-options -->
<a id='snippet-run-options'></a>
```cs
WithRunOptions()
    .WithMaxToolIterations(10)
    .WithConcurrentToolCalls(true)
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L18-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Run options set the [mode](./tool-approval.md#the-three-modes) a run starts in, the limits of its tool loop, whether
the calls of one response run at the same time, and when the history is [compacted](./conversations.md#compaction).
[Builders](./builders.md#runoptionsbuilder) lists each one with its default.

## Tool settings

`Configure{Tool}()` sets a tool's [settings](./tool-settings.md) for this agent, over the record's code defaults, as
`ConfigureSearchTickets().WithLimit(5)` does above. It can set every settable member of the record, configurable or
not, for every run of this agent. A tool the agent does not offer has none, so configuring it does not compile.
[Generated members](./generated-members.md#configure-tool) lists what the builder offers.

## The generated settings

Every agent gets a nested `Settings` record, here `SupportAgent.Settings`, holding exactly what may change at
runtime:

| Member | From |
|---|---|
| `Provider`, `Model` | Present when `Configurable(o => o.ModelId)` allows them. They override what `WithModel` sets. |
| One per configurable chat option | `Instructions` and `Temperature` above. |
| One per configurable run option | `Mode` and `MaxToolIterations` from the run options above. |
| One per offered tool with configurable settings | `SearchTickets`, `SearchArticles`: their [overrides](./tool-settings.md#the-generated-overrides). |

Option values are nullable, and so are the values in a tool's overrides. A null keeps the
[layer below](./settings-and-stores.md#settings-layering). When a tool's member would be named like an option or like
one of the record's own members, such as `Kind`, the generator adds a `Tool` suffix to it.

Store the record [under a settings id](./settings-and-stores.md#agent-settings), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. The record's `Serialize()`
and `System.Text.Json` write the same JSON, so either works for an API. In a store, `WithStorageKind` sets the name
the record is kept under in place of the class name, and `WithSettingsVersion` raises its version after you rename or
remove a configurable member ([Settings documents](./implementing-a-store.md#settings-documents)).

## Related

- [Toolsets](./toolsets.md): declaring the tools an agent offers.
- [Tool settings](./tool-settings.md): the record a `Configure{Tool}()` sets.
- [Running agents](./running-agents.md): running an agent through its host.
- [Builders](./builders.md#agent): every call an agent's constructor can make.
- [Generated members](./generated-members.md#per-agent): what the generator writes for each agent.
