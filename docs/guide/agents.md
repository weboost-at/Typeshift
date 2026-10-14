# Agents

An agent bundles what a model needs for one job: chat options, instructions, tools and their settings. Like a
toolset, it is declared in its constructor and read at compile time.

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `Agent`:

<!-- snippet: agent -->
<!-- endSnippet -->

- **`WithName`** is optional; without it the agent goes by its class name in snake_case, `support_agent`. Messages the
  agent writes carry it as their author.
- **`WithDescription`** says what the agent does.
- **`WithChatOptions()`** sets the options every run starts from: `WithModel`, `WithTemperature`,
  `WithMaxOutputTokens` and one `With...` per other option. `WithTools` adds tools of no toolset, such as a hosted
  web search.
- **`GetInstructions()`** returns the instructions. It runs whenever a run's options are resolved, so the text may be
  computed.

`WithName` and `WithDescription` take constants ([TSH002](./diagnostics.md#tsh002)). Every declaration goes directly
in the constructor, one chain per statement ([TSH014](./diagnostics.md#tsh014),
[TSH015](./diagnostics.md#tsh015)). An agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)); share
declarations through plain static methods and constants.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only some, in that order, or `Exclude` to
offer all but some:

<!-- snippet: exclude-tools -->
<!-- endSnippet -->

The lambda picks from a list the generator writes per toolset, one property per tool: `search_tickets` is
`t.SearchTickets`. A tool of another toolset is not on the list, so picking it does not compile. Use each toolset
once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). A toolset of another project needs the package in
that project ([TSH019](./diagnostics.md#tsh019)).

## Context providers

`UseContext<T>()` adds what a provider returns to every run: instructions, messages ahead of the input, tools. See
[context providers](./context-providers.md).

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that a run may change, whether the code sets it or not. The model
is an option like any other: `Configurable(o => o.ModelId)` lets settings choose it, so a settings form never offers
a model the agent does not allow. The model, the instructions, the sampling options (`Temperature`,
`MaxOutputTokens`, `TopP`, `TopK`, the penalties, `Seed`), `StopSequences`, `AllowMultipleToolCalls` and
`Reasoning` may be configurable; the rest belongs to the run, not to settings ([TSH012](./diagnostics.md#tsh012)).

## Run options

`WithRunOptions()` sets how a run's tool loop goes: the [mode](./modes.md) it starts in, the most rounds of tool
calls a run makes (40 unless set), the most tool calls in a row that may fail (3), whether the calls of one response
run at the same time, and when the history is [compacted](./long-running-agents.md#compaction). `Configurable` marks
those a run may change:

<!-- snippet: run-options -->
<!-- endSnippet -->

## Tool settings

`Configure{Tool}()` sets a tool's settings in this agent, over the record's code defaults:
`ConfigureSearchTickets().WithLimit(5)`. The generator writes one per offered tool with settings, with a `With...`
per settable member, configurable or not. A tool the agent does not offer has none, so configuring it does not
compile. A `Configure{Tool}()` the agent declares itself takes the place of the generated one.

## The generated settings

Every agent gets a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may change at runtime:

| Member | From |
|---|---|
| `Model` | When `Configurable(o => o.ModelId)` allows it; overrides `WithModel`. |
| One per configurable chat option | `Instructions` and `Temperature` above. |
| One per configurable run option | `MaxToolIterations` of the triage agent above. |
| One per offered tool with configurable settings | `SearchTickets`, `SearchArticles`: their [overrides](./tool-settings.md#the-generated-overrides). |

Every value is nullable, and null keeps the layer below. A tool member named like an option gets a `Tool` suffix. Store
the record [under a settings id](./settings-and-stores.md#agent-settings), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. Its JSON is the same through
`Serialize()` and through `System.Text.Json`. `WithStorageKind` and `WithSettingsVersion` name and version it in a
store.
