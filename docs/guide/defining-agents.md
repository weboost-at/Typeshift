---
title: Agents
description: Declare an agent in its constructor, with its chat options, instructions, tools and their settings.
---

# Agents

An agent bundles what a model needs for one job: chat options, instructions and tools. You declare it in its
constructor, and the generator gives it a `Settings` record of what may change at runtime.

## Declaring an agent

An agent is a `partial` class ([TSH001](./diagnostics.md#tsh001)) deriving from `Agent`. This one answers customers
from their tickets and the help center:

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

The tools and their settings have sections of their own below. The other calls of this agent declare the rest:

| Call | Declares |
|---|---|
| `WithDescription(...)` | What the agent does, for the model of an agent that calls this one. |
| `WithChatOptions()` | The chat options every run starts from, such as the model and the temperature. [Models and providers](./models.md) explains what `WithModel` names and which client serves it. |
| `UseMcpServer(...)` | The tools of an [MCP server](./mcp.md). `ReadOnly()` lets plan mode offer them. |
| `UseContext<T>()` | A [context provider](./middleware.md#context-providers), whose instructions, messages and tools join every run. |
| `GetInstructions()` | The instructions, the agent's system prompt, as an override for text your code builds. `WithInstructions(...)` in the constructor suits instructions that are the same for every run. |

[Builders](./builders.md#agent) lists every call an agent's constructor can make, with what each takes. Among them are
`WithName`, for an agent that is not to be named after its class, and `UseAgent<T>()` and `UseHandoff<T>()`, which
offer [other agents](./multi-agent.md).

`WithName` and `WithDescription` take constants ([TSH002](./diagnostics.md#tsh002)), and every declaration sits directly
in the constructor as one chain ([TSH014](./diagnostics.md#tsh014), [TSH015](./diagnostics.md#tsh015)). An agent derives
from `Agent` directly, never from a base agent of yours ([TSH017](./diagnostics.md#tsh017)). To share between agents,
put values in constants and code in static methods, and keep every declaration in each agent's constructor.
[How Typeshift reads your code](./how-typeshift-reads-your-code.md) explains these rules, and why you cannot define an
agent at runtime.

Typeshift creates each agent once, with `new`, so the class needs a public or internal parameterless constructor and
cannot be nested as `private` or `protected` ([TSH016](./diagnostics.md#tsh016)). Its constructor therefore takes no
services. The agent's tools reach them through their [`IServiceProvider` parameter](./toolsets.md#parameters).

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
        WithInstructions("Settle the customer's charges and invoices.");

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
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/BillingAgent.cs#L3-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-own-tool' title='Start of snippet'>anchor</a></sup>
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

`Configure{Tool}()` sets a tool's [settings](./tool-settings.md) for every run of this agent, over the record's code
defaults, as `ConfigureSearchTickets().WithLimit(5)` does above.
[Generated members](./generated-members.md#configure-tool) lists what its builder offers and which tools have one.

## The generated settings

Every agent gets a nested `Settings` record, here `SupportAgent.Settings`, holding exactly what may change at
runtime: the chat and run options you marked configurable, and the
[overrides](./tool-settings.md#the-generated-overrides) of each offered tool that has configurable settings. A value
you leave null keeps the [layer below](./settings-and-stores.md#settings-layering).
[Generated members](./generated-members.md#settings) lists the record's members and the declaration each comes from.

Store the record [under a settings id](./settings-and-stores.md#settings-under-an-id-such-as-per-customer), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. The record's `Serialize()`
and `System.Text.Json` write the same JSON, so either works for an API. In a store, `WithStorageKind` sets the name
the record is kept under in place of the class name, and `WithSchemaVersion` raises its schema version after you rename
or remove a configurable member ([Migrate older settings](./implementing-a-store.md#migrate-older-settings)).

## Related

- [Toolsets](./toolsets.md): declaring the tools an agent offers.
- [Tool settings](./tool-settings.md): the record a `Configure{Tool}()` sets.
- [Sub-agents and handoffs](./multi-agent.md): offering another agent as a tool, or handing a conversation to it.
- [Models and providers](./models.md): the client that serves the model an agent names.
- [Running an agent](./running-agents.md): what a run takes and returns.
- [Builders](./builders.md#agent): every call an agent's constructor can make.
- [Generated members](./generated-members.md#per-agent): what the generator writes for each agent.
