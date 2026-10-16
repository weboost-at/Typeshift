---
title: Builders
description: Every call a toolset or agent constructor can make, and the chat and run options with their defaults.
---

# Builders

Toolsets and agents declare themselves in their constructors, and this page lists every call they can make. To learn
the calls in context, read [Toolsets](./toolsets.md), [Tool settings](./tool-settings.md) and
[Agents](./defining-agents.md).

## Rules for every chain

The generator reads each chain from your source at compile time. At runtime the calls only record values. So every
declaration sits directly in the constructor ([TSH014](./diagnostics.md#tsh014)) and is one chain in one statement
([TSH015](./diagnostics.md#tsh015)). Names, descriptions, the storage kind and the settings version are constants
([TSH002](./diagnostics.md#tsh002)).

## Toolset

| Call | Does |
|---|---|
| `WithDisplayName(string displayName)` | Names the toolset for people, for example in a tool picker. `DisplayName` reads it back. |
| `AddTool(Delegate method)` | Declares a tool backed by a static method group, such as `AddTool(SearchTickets)`, and returns its [`ToolBuilder`](#toolbuilder). A lambda, an instance method or a local function is [TSH003](./diagnostics.md#tsh003). |

A tool's [name and description](./toolsets.md#names-and-descriptions), the [parameters](./toolsets.md#parameters) the
call fills and the [results](./toolsets.md#results) it may return are on [Toolsets](./toolsets.md).

## ToolBuilder

| Call | Does |
|---|---|
| `WithName(string name)` | Replaces the name derived from the method. It must be lower_snake_case ([TSH008](./diagnostics.md#tsh008)). |
| `WithDescription(string description)` | Replaces the `<summary>`. A tool with neither is [TSH009](./diagnostics.md#tsh009). |
| `RequiresApproval()` | Pauses a run that calls the tool until the host approves or rejects the call. See [Tool approval](./tool-approval.md). |
| `ReadOnly()` | Marks a tool that only reads and changes nothing. [Plan mode](./modes.md#plan-mode) offers only these. |
| `WithSettings<TSettings>()` | Gives the tool a settings record that stays in code, except the members `Configurable` names. Returns `ToolBuilder<TSettings>`. |
| `WithConfigurableSettings<TSettings>()` | Gives the tool a settings record whose settable members may all change, except those `NotConfigurable` names. Returns `ConfigurableSettingsBuilder<TSettings>`. |

The settings call comes last. `TSettings` is a record whose parameterless constructor sets the code defaults
([TSH005](./diagnostics.md#tsh005)), and the tool's method takes a parameter of that type
([TSH010](./diagnostics.md#tsh010)).

### Configurable members

| Call | On | Does |
|---|---|---|
| `Configurable(s => s.Member)` | `ToolBuilder<TSettings>` | Lets the member change at runtime, through stored settings or a run. |
| `NotConfigurable(s => s.Member)` | `ConfigurableSettingsBuilder<TSettings>` | Keeps the member in code. |

The lambdas never run, so a pick must be `s => s.Member` ([TSH015](./diagnostics.md#tsh015)). A configurable member
needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)) and a type settings can store
([TSH012](./diagnostics.md#tsh012)), which [Tool settings](./tool-settings.md#configurable-members) lists.
`WithConfigurableSettings` leaves obsolete members out on its own.

## Agent

| Call | Does |
|---|---|
| `WithName(string name)` | Names the agent, which is otherwise its class name in snake_case. Messages the agent writes carry the name as their author. |
| `WithDescription(string description)` | Says what the agent does. A sub-agent or a handoff target needs one ([TSH023](./diagnostics.md#tsh023)). |
| `WithStorageKind(string kind)` | Sets the kind a store keys the agent's `Settings` by, in place of its class name. |
| `WithSettingsVersion(int version)` | Sets the schema version of the stored `Settings`, 1 unless raised. |
| `WithChatOptions()` | Returns the [`ChatOptionsBuilder`](#chatoptionsbuilder). |
| `WithRunOptions()` | Returns the [`RunOptionsBuilder`](#runoptionsbuilder). |
| `UseToolset<TToolset>()` | Offers every tool of the toolset. `.Include(t => t.X)` offers only the tools picked, in that order, and `.Exclude(t => t.X)` all but those. One use includes or excludes, never both. Use each toolset once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). |
| `UseAgent<TAgent>()` | Offers another agent as a tool named like it, with one `input` parameter. `.InBackground()` returns at once and delivers the answer later. See [Sub-agents](./multi-agent.md#sub-agents). |
| `UseHandoff<TAgent>()` | Offers a tool `handoff_to_{name}` that hands the conversation to the other agent. See [Handoff](./multi-agent.md#handoff). |
| `UseMcpServer(string server)` | Offers the tools of an MCP server that the host's `IMcpToolProvider` connects. `.RequiresApproval()` asks before every call of its tools, and `.ReadOnly()` lets plan mode offer them. |
| `UseContext<TProvider>()` | Adds what an `IContextProvider` returns to every run. The run's services resolve the provider, or create it when none is registered. |

The agent's instructions come from `GetInstructions()`, a virtual method you override. It runs whenever a run's
options are resolved. An agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)).

## ChatOptionsBuilder

`WithChatOptions()` sets the chat options every run starts from. `Configurable(o => o.X)` lets stored settings or a
run change an option, whether the code sets it or not, and makes it a member of the agent's
[`Settings`](./generated-members.md#settings). An option marked "No" belongs to the run or has a type settings cannot
store, so it cannot be configurable ([TSH012](./diagnostics.md#tsh012)).

| Call | Sets | May be configurable |
|---|---|---|
| `WithModel(string model)` | `ModelId`, as the `IChatClientProvider` names models | Yes, as `Model` |
| `GetInstructions()`, overridden in the agent | `Instructions` | Yes |
| `WithTemperature(float temperature)` | `Temperature` | Yes |
| `WithMaxOutputTokens(int tokens)` | `MaxOutputTokens` | Yes |
| `WithTopP(float topP)` | `TopP` | Yes |
| `WithTopK(int topK)` | `TopK` | Yes |
| `WithFrequencyPenalty(float penalty)` | `FrequencyPenalty` | Yes |
| `WithPresencePenalty(float penalty)` | `PresencePenalty` | Yes |
| `WithSeed(long seed)` | `Seed` | Yes |
| `WithStopSequences(params string[] sequences)` | `StopSequences` | Yes |
| `WithAllowMultipleToolCalls(bool allow)` | `AllowMultipleToolCalls` | Yes |
| `WithReasoning(ReasoningOptions reasoning)` | `Reasoning` | Yes |
| `WithResponseFormat(ChatResponseFormat format)` | `ResponseFormat` | No |
| `WithToolMode(ChatToolMode mode)` | `ToolMode` | No |
| `WithTools(params AITool[] tools)` | Adds tools of no toolset to `Tools`, such as a `HostedWebSearchTool` | No |
| `WithAdditionalProperty<TValue>(string key, TValue value)` | A provider-specific option in `AdditionalProperties` | No |

## RunOptionsBuilder

`WithRunOptions()` sets how a run's tool loop goes. Every run option may be configurable: `Configurable(r => r.X)`
puts it into the agent's `Settings`, like a chat option.

| Call | Default | Does |
|---|---|---|
| `WithMode(AgentMode mode)` | `Default` | Sets the [mode](./modes.md) runs start in: `Default`, `Plan` or `Auto`. |
| `WithMaxToolIterations(int iterations)` | 40 | Sets the most rounds of tool calls one run makes. |
| `WithMaxConsecutiveToolErrors(int errors)` | 3 | Sets the most failed tool calls in a row before the run ends. |
| `WithConcurrentToolCalls(bool concurrent)` | off | Runs the tool calls of one model response at the same time. |
| `WithMaxInputTokens(int tokens)` | off | Turns [compaction](./long-running-agents.md#compaction) on at this many input tokens. |
| `WithTargetInputTokens(int tokens)` | half the maximum | Sets how far compaction shrinks the history. |
| `WithSummaryModel(string model)` | none | Sets the model that summarizes what clearing tool results did not free. Without one, compaction drops the oldest turns. |

## Related

- [Toolsets](./toolsets.md): declaring tools, and their names, parameters and results.
- [Tool settings](./tool-settings.md): settings records and their configurable members.
- [Agents](./defining-agents.md): declaring an agent and choosing its tools.
- [Generated members](./generated-members.md): what the generator writes from these chains.
- [Diagnostics](./diagnostics.md): what the analyzer reports when a chain breaks a rule.
