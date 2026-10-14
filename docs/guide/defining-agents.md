---
title: Agents
description: Declare an agent in its constructor, with its chat options, instructions, tools and their settings.
---

# Agents

An agent bundles what a model needs for one job: chat options, instructions, tools and their settings. Like a toolset,
it is declared in its constructor and read at compile time. The generator then gives it a `Settings` record with exactly
what may change at runtime.

::: tip The short version

- An agent is a `partial` class deriving from `Agent`. Its constructor declares everything, and `GetInstructions()`
  returns the instructions.
- `WithChatOptions()` sets the model and the options every run starts from. `WithRunOptions()` sets how the tool loop
  goes.
- `UseToolset<T>()` offers a toolset's tools, and `Include` or `Exclude` picks some of them.
- `Configure{Tool}()` sets a tool's settings for this agent.
- `Configurable(...)` marks what stored settings and runs may change. Those members make up the agent's `Settings`.

:::

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `Agent`:

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
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithName`** is optional. Without it, the agent goes by its class name in snake_case, here `support_agent`. Messages
  the agent writes carry the name as their author.
- **`WithDescription`** says what the agent does.
- **`WithChatOptions()`** sets the options every run starts from: `WithModel`, `WithTemperature`, `WithMaxOutputTokens`
  and one `With...` per other option. `WithTools` adds tools of no toolset, such as a hosted web search.
- **`GetInstructions()`** returns the instructions. It runs whenever a run's options are resolved, so the text may be
  computed.

The generator reads the constructor at compile time, so a few rules hold:

- `WithName` and `WithDescription` take constants ([TSH002](./diagnostics.md#tsh002)).
- Every declaration goes directly in the constructor, one chain per statement ([TSH014](./diagnostics.md#tsh014),
  [TSH015](./diagnostics.md#tsh015)).
- An agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)). Share declarations through plain static
  methods and constants.

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

The lambda picks from a list the generator writes per toolset, with one property per tool: `search_tickets` is
`t.SearchTickets`. A tool of another toolset is not on the list, so picking it does not compile.

- Use each toolset once and pick each tool once ([TSH025](./diagnostics.md#tsh025)).
- A toolset of another project needs the package in that project ([TSH019](./diagnostics.md#tsh019)).

## Other agents

`UseAgent<T>()` offers another agent as a tool. `UseHandoff<T>()` lets the model hand the conversation to it. See
[Multiple agents](./multi-agent.md).

## MCP servers

`UseMcpServer("status_page")` offers the tools of an MCP server, and your `IMcpToolProvider` connects it. `ReadOnly()`
lets plan mode offer them. See [MCP servers](./mcp.md).

## Context providers

`UseContext<T>()` adds what a provider returns to every run: instructions, messages ahead of the input and tools. See
[Context providers](./context-providers.md).

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that stored settings and runs may change, whether the code sets it or
not. The model is an option like any other: `Configurable(o => o.ModelId)` lets settings choose it, so a settings form
never offers a model the agent does not allow. [Builders](./builders.md#chatoptionsbuilder) lists the options that may
be configurable. The rest belong to the run, not to settings ([TSH012](./diagnostics.md#tsh012)).

## Run options

`WithRunOptions()` sets how a run's tool loop goes. `Configurable` marks the options a run may change:

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

Run options set the [mode](./modes.md) a run starts in, the limits of its tool loop, whether the calls of one response
run at the same time, and when the history is [compacted](./long-running-agents.md#compaction).
[Builders](./builders.md#runoptionsbuilder) lists each option with its default.

## Tool settings

`Configure{Tool}()` sets a tool's settings in this agent, over the record's code defaults:
`ConfigureSearchTickets().WithLimit(5)`. The generator writes one per offered tool with settings, with a `With...` per
settable member, configurable or not.

- A tool the agent does not offer has no `Configure{Tool}()`, so configuring it does not compile.
- A `Configure{Tool}()` the agent declares itself takes the place of the generated one.

See [Tool settings](./tool-settings.md) for the settings record itself.

## The generated settings

Every agent gets a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may change at runtime:

| Member | From |
|---|---|
| `Model` | When `Configurable(o => o.ModelId)` allows it; overrides `WithModel`. |
| One per configurable chat option | `Instructions` and `Temperature` above. |
| One per configurable run option | `MaxToolIterations` of the triage agent above. |
| One per offered tool with configurable settings | `SearchTickets`, `SearchArticles`: their [overrides](./tool-settings.md#the-generated-overrides). |

Every value is nullable, and null keeps the [layer below](./settings-layering.md). A tool member named like an option
gets a `Tool` suffix.

You can store the record [under a settings id](./settings-and-stores.md#agent-settings), pass it [for one
run](./running-agents.md#run-settings), or bind a settings form or an API to it. Its JSON is the same through
`Serialize()` and through `System.Text.Json`. `WithStorageKind` and `WithSettingsVersion` name and version it in a
store.

## Related

- [Toolsets](./toolsets.md): declaring the tools an agent offers.
- [Tool settings](./tool-settings.md): the record a `Configure{Tool}()` sets.
- [Running agents](./running-agents.md): running an agent through its host.
- [Builders](./builders.md#agent): every call an agent's constructor can make.
- [Generated members](./generated-members.md#per-agent): what the generator writes for each agent.
