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
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page").ReadOnly();

        UseContext<CustomerProfileContext>();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The tools and their settings have sections of their own below. The other calls declare the rest:

| Call | Declares |
|---|---|
| `WithName(...)` | The name that messages the agent writes carry as their author. Optional: `SupportAgent` is `support_agent`. |
| `WithDescription(...)` | What the agent does. The model of an agent that calls this one reads it. |
| `WithChatOptions()` | The options every run starts from: `WithModel`, `WithTemperature`, `WithMaxOutputTokens` and a `With...` per other option. `WithTools` adds tools of no toolset, such as a hosted web search. |
| `UseMcpServer(...)` | The tools of an [MCP server](./mcp.md). `ReadOnly()` lets plan mode offer them. |
| `UseContext<T>()` | A [context provider](./context-providers.md), whose instructions, messages and tools join every run. |
| `UseAgent<T>()`, `UseHandoff<T>()` | Another agent as a tool, or one the model can hand the conversation to. See [Multiple agents](./multi-agent.md). |
| `GetInstructions()` | The instructions, as an override. It runs whenever a run's options are resolved, so the text may be computed. |

Because the generator reads the constructor rather than running it, `WithName` and `WithDescription` take constants
([TSH002](./diagnostics.md#tsh002)), and every declaration sits directly in the constructor as one chain
([TSH014](./diagnostics.md#tsh014), [TSH015](./diagnostics.md#tsh015)). An agent derives from `Agent` directly
([TSH017](./diagnostics.md#tsh017)), so share declarations through static methods and constants instead.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only some, in that order, or `Exclude` to
offer all but some:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The lambda picks from a list the generator writes for each toolset, with one property per tool: `search_tickets` is
`t.SearchTickets`. A tool of another toolset is not on that list, so picking it does not compile. Use each toolset
once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). A toolset from another project needs the Typeshift
package in that project ([TSH019](./diagnostics.md#tsh019)).

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that stored settings and runs may change, whether the code sets it
or not. The model is an option like any other, so without `Configurable(o => o.ModelId)` a settings form never offers
a model the agent does not allow.

[Builders](./builders.md#chatoptionsbuilder) lists which options may be configurable. The rest belong to the run
([TSH012](./diagnostics.md#tsh012)).

## Run options

`WithRunOptions()` sets how a run's tool loop goes, and `Configurable` marks the options a run may change:

<!-- snippet: run-options -->
<a id='snippet-run-options'></a>
```cs
WithRunOptions()
    .WithMaxToolIterations(10)
    .WithConcurrentToolCalls(true)
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Run options set the [mode](./modes.md) a run starts in, the limits of its tool loop, whether
the calls of one response run at the same time, and when the history is [compacted](./long-running-agents.md#compaction).
[Builders](./builders.md#runoptionsbuilder) lists each one with its default.

## Tool settings

`Configure{Tool}()` sets a tool's [settings](./tool-settings.md) for this agent, over the record's code defaults, as
`ConfigureSearchTickets().WithLimit(5)` does above. A tool the agent does not offer has none, so configuring it does
not compile. [Generated members](./generated-members.md#configure-tool) lists what the builder offers.

## The generated settings

Every agent gets a nested `Settings` record, here `SupportAgent.Settings`, holding exactly what may change at
runtime:

| Member | From |
|---|---|
| `Model` | When `Configurable(o => o.ModelId)` allows it; overrides `WithModel`. |
| One per configurable chat option | `Instructions` and `Temperature` above. |
| One per configurable run option | `MaxToolIterations` of the triage agent above. |
| One per offered tool with configurable settings | `SearchTickets`, `SearchArticles`: their [overrides](./tool-settings.md#the-generated-overrides). |

Option values are nullable, and so are the values in a tool's overrides. A null keeps the
[layer below](./settings-and-stores.md#settings-layering). A tool member named like an option, or like a member of
the record itself, gets a `Tool` suffix.

Store the record [under a settings id](./settings-and-stores.md#agent-settings), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. `Serialize()` and
`System.Text.Json` write the same JSON. `WithStorageKind` and `WithSettingsVersion` name and version it in a store.

## Related

- [Toolsets](./toolsets.md): declaring the tools an agent offers.
- [Tool settings](./tool-settings.md): the record a `Configure{Tool}()` sets.
- [Running agents](./running-agents.md): running an agent through its host.
- [Builders](./builders.md#agent): every call an agent's constructor can make.
- [Generated members](./generated-members.md#per-agent): what the generator writes for each agent.
