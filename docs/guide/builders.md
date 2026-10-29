---
title: Builders
description: Every call a toolset or agent constructor can make, and the chat and run options with their defaults.
---

# Builders

Toolsets and agents declare themselves in their constructors, and this page lists every call they can make. To learn
the calls in context, read [Toolsets](./toolsets.md), [Tool settings](./tool-settings.md) and
[Agents](./defining-agents.md).

## Rules for every chain

The generator reads each chain from your source at compile time, and at runtime the calls only record values. Because
the generator reads source, put every declaration directly in the constructor ([TSH014](./diagnostics.md#tsh014)) and
write it as one chain in one statement ([TSH015](./diagnostics.md#tsh015)). Names, descriptions, the storage kind and
the settings version must be constants ([TSH002](./diagnostics.md#tsh002)). Each toolset derives from `Toolset` and
each agent from `Agent` directly ([TSH017](./diagnostics.md#tsh017)).

## Toolset

| Call | Does |
|---|---|
| `WithDisplayName(string displayName)` | Names the toolset for people, for example in a tool picker. `DisplayName` reads it back. |
| `AddTool(Delegate method)` | Declares a tool backed by a static method group, such as `AddTool(SearchTickets)`, and returns its [`ToolBuilder`](#toolbuilder). For a lambda, an instance method or a local function, the analyzer reports [TSH003](./diagnostics.md#tsh003). |

[Toolsets](./toolsets.md) explains a tool's [name and description](./toolsets.md#names-and-descriptions), its
[parameters](./toolsets.md#parameters), including those the call fills instead of the model, and the
[results](./toolsets.md#results) it may return.

## ToolBuilder

| Call | Does |
|---|---|
| `WithName(string name)` | Replaces the name derived from the method. It must be lower_snake_case ([TSH008](./diagnostics.md#tsh008)). |
| `WithDescription(string description)` | Replaces the `<summary>`. For a tool with neither, the analyzer reports [TSH009](./diagnostics.md#tsh009). |
| `RequiresApproval()` | Makes the tool's calls need approval. Unless a rule, a standing approval or auto mode decides a call, the run ends, and the call runs once your app approves it in the conversation's next run. See [Approval and modes](./tool-approval.md). |
| `ReadOnly()` | Marks a tool that only reads and changes nothing. [Plan mode](./tool-approval.md#plan-mode) offers only these. |
| `WithSettings<TSettings>()` | Gives the tool a settings record that stays in code, except the members `Configurable` names. Returns `ToolBuilder<TSettings>`. |
| `WithConfigurableSettings<TSettings>()` | Gives the tool a settings record whose settable members may all change, except those `NotConfigurable` names. Returns `ConfigurableSettingsBuilder<TSettings>`. |

Put the settings call last in the tool's chain, because it returns `ToolBuilder<TSettings>` or
`ConfigurableSettingsBuilder<TSettings>`, which offer only the calls below: `ReadOnly()` after it is a compiler error
(CS1061). `TSettings` is a record whose
parameterless constructor sets the code defaults ([TSH005](./diagnostics.md#tsh005)), and the tool's method takes a
parameter of that type ([TSH010](./diagnostics.md#tsh010)).

### Configurable members

| Call | On | Does |
|---|---|---|
| `Configurable(s => s.Member)` | `ToolBuilder<TSettings>` | Lets the member change at runtime, through stored settings or a run. |
| `NotConfigurable(s => s.Member)` | `ConfigurableSettingsBuilder<TSettings>` | Keeps the member in code. |

The generator reads these lambdas and never runs them, so each one must name a member directly, as in `s => s.Member`
([TSH015](./diagnostics.md#tsh015)). A configurable member needs a public `set` or `init` accessor
([TSH013](./diagnostics.md#tsh013)) and a type that settings can store ([TSH012](./diagnostics.md#tsh012));
[Tool settings](./tool-settings.md#configurable-members) lists those types. `WithConfigurableSettings` leaves members
marked `[Obsolete]` out without a `NotConfigurable` call.

## Agent

| Call | Does |
|---|---|
| `WithName(string name)` | Names the agent, which is otherwise its class name in snake_case. Messages the agent writes carry the name as their author. |
| `WithDescription(string description)` | Says what the agent does. A sub-agent or a handoff target needs one ([TSH023](./diagnostics.md#tsh023)). |
| `WithStorageKind(string kind)` | Sets the kind a store keys the agent's `Settings` by, in place of its class name, so renaming the class keeps its stored settings. |
| `WithSettingsVersion(int version)` | Sets the schema version of the stored `Settings`, 1 unless raised. Raise it after you rename or remove a configurable member, so the store can migrate older documents. |
| `WithChatOptions()` | Returns the [`ChatOptionsBuilder`](#chatoptionsbuilder). |
| `WithRunOptions()` | Returns the [`RunOptionsBuilder`](#runoptionsbuilder). |
| `UseToolset<TToolset>()` | Offers every tool of the toolset. `.Include(t => t.X)` offers only the tools picked, in that order, and `.Exclude(t => t.X)` all but those. One use includes or excludes, never both. Use each toolset once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). |
| `AddTool(Delegate method)` | Declares a tool of the agent's own, backed by a static method of the agent, and returns its [`ToolBuilder`](#toolbuilder). The agent offers it after the tools of its toolsets. See [Agents](./defining-agents.md#add-a-tool-of-the-agent-s-own). |
| `UseAgent<TAgent>()` | Offers another agent as a tool named like it, with one `input` parameter. `.InBackground()` returns at once and delivers the answer later. See [Sub-agents](./multi-agent.md#sub-agents). |
| `UseHandoff<TAgent>()` | Offers a tool `handoff_to_{name}` that hands the conversation to the other agent. See [Handoff](./multi-agent.md#handoff). |
| `UseMcpServer(string server)` | Offers the tools of an MCP server that your app's `IMcpToolProvider` connects. `.RequiresApproval()` makes every call to its tools wait for approval, and `.ReadOnly()` lets plan mode offer them. |
| `UseContext<TProvider>()` | Adds what an `IContextProvider` returns to every run. The run's services resolve the provider, or create it when none is registered. |

The agent's instructions come from `GetInstructions()`, a virtual method you override. Typeshift calls it each time it
resolves a run's options, so it runs for every run.

## ChatOptionsBuilder

`WithChatOptions()` sets the chat options every run starts from. `Configurable(o => o.X)` lets stored settings or a
run change an option, even one the code never sets, and makes it a member of the agent's
[`Settings`](./generated-members.md#settings). An option with "No" in the last column cannot be configurable
([TSH012](./diagnostics.md#tsh012)): it belongs to the run, as the tools do, or settings cannot store its type.

| Call | Sets | May be configurable |
|---|---|---|
| `WithModel(TProviderId provider, string model)` | `ModelId`, and the [provider](./models.md) that serves it | Yes, as `Provider` and `Model` |
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
| `WithTools(params AITool[] tools)` | Adds tools of no toolset to `Tools`, such as a `HostedWebSearchTool` or a function from `AIFunctionFactory` | No |
| `WithAdditionalProperty<TValue>(string key, TValue value)` | A provider-specific option in `AdditionalProperties` | No |

## RunOptionsBuilder

`WithRunOptions()` sets how a run goes: its mode, the limits of its tool loop and when its history is compacted. Every
run option may be configurable: `Configurable(r => r.X)` puts it into the agent's `Settings`, like a chat option.

| Call | Default | Does |
|---|---|---|
| `WithMode(AgentMode mode)` | `Default` | Sets the [mode](./tool-approval.md#the-three-modes) runs start in: `Default`, `Plan` or `Auto`. |
| `WithMaxToolIterations(int iterations)` | 40 | Sets the most rounds of tool calls one run makes. Calls your app [approves](./tool-approval.md#approving-or-rejecting) run in the next run, which counts its own. |
| `WithMaxConsecutiveToolErrors(int errors)` | 3 | Sets how many rounds of tool calls in a row may fail before the failing tool's exception ends the run and reaches your code. |
| `WithConcurrentToolCalls(bool concurrent)` | off | Runs the tool calls in one model response at the same time. |
| `WithMaxInputTokens(int tokens)` | off | Turns [compaction](./conversations.md#compaction) on at this many input tokens. |
| `WithTargetInputTokens(int tokens)` | half the maximum | Sets how far compaction shrinks the history. |
| `WithSummaryModel(string model)` | the run's model | Sets the model that summarizes the oldest turns when clearing old tool results does not shrink the history enough, served by the run's provider. Made configurable, it also adds `SummaryProvider` to `Settings` ([the summary model](./models.md#the-summary-model)). |

## Related

- [Toolsets](./toolsets.md): declaring tools, and their names, parameters and results.
- [Tool settings](./tool-settings.md): settings records and their configurable members.
- [Agents](./defining-agents.md): declaring an agent and choosing its tools.
- [Generated members](./generated-members.md): what the generator writes from these chains.
- [Diagnostics](./diagnostics.md): what the analyzer reports when a chain breaks a rule.
