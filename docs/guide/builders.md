---
title: Builders
description: Every call a toolset or agent constructor can make, and the chat and run options with their defaults.
---

# Builders

Toolsets and agents declare themselves in their constructors, and this page lists every call they can make. To learn
the calls in context, read [Toolsets](./toolsets.md), [Tool settings](./tool-settings.md) and
[Agents](./defining-agents.md).

## Rules for every chain

Every chain follows these rules, and [How Typeshift reads your code](./how-typeshift-reads-your-code.md) explains why:

- Every declaration sits directly in the constructor ([TSH014](./diagnostics.md#tsh014)): the parameterless one, or
  one it chains to with `this(...)`, since Typeshift runs no other ([TSH044](./diagnostics.md#tsh044)).
- Each declaration is one chain in one statement ([TSH015](./diagnostics.md#tsh015)).
- Names, descriptions, the storage kind and the schema version are constants ([TSH002](./diagnostics.md#tsh002)).
- A declaration names constants, methods and types of your own code, not ones a source generator writes
  ([TSH032](./diagnostics.md#tsh032)).
- Each toolset derives from `Toolset` and each agent from `Agent` directly, and names that base itself, not through a
  using alias ([TSH017](./diagnostics.md#tsh017)).
- Each toolset and agent is a `partial` class, and so is every class it is nested in
  ([TSH001](./diagnostics.md#tsh001)).
- Each has a public or internal parameterless constructor, is not nested as `private` or `protected` and is no
  `file` class ([TSH016](./diagnostics.md#tsh016)).

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
| `RequiresApproval()` | Makes the tool's calls need approval. Unless a rule, a standing approval or auto mode decides a call, the run ends with a decision, and the call runs once your app approves it with `AnswerAsync`. See [Approval and modes](./tool-approval.md). |
| `ReadOnly()` | Marks a tool that only reads and changes nothing. [Plan mode](./tool-approval.md#plan-mode) offers only these, and [compaction](./conversations.md#compaction) tells the model to call only these again for a result it cleared. |
| `WithSettings<TSettings>()` | Gives the tool a settings record that stays in code, except the members `Configurable` names. Returns `ToolBuilder<TSettings>`. |
| `WithConfigurableSettings<TSettings>()` | Gives the tool a settings record whose settable members may all change, except those `NotConfigurable` names. Returns `ConfigurableSettingsBuilder<TSettings>`. |

The calls of a tool's chain come in any order: after a settings call the chain offers the calls above as well as
those below, so `RequiresApproval()` may follow `Configurable(s => s.Limit)`. `TSettings` is a record whose
parameterless constructor sets the code defaults ([TSH005](./diagnostics.md#tsh005)), and the tool's method takes a
parameter of that type ([TSH010](./diagnostics.md#tsh010)).

### Configurable members

| Call | On | Does |
|---|---|---|
| `Configurable(s => s.Member)` | `ToolBuilder<TSettings>` | Lets the member change at runtime, through stored settings or a run. |
| `NotConfigurable(s => s.Member)` | `ConfigurableSettingsBuilder<TSettings>` | Keeps the member in code. |

Each of these lambdas names a member directly, as in `s => s.Member` ([TSH015](./diagnostics.md#tsh015)). A configurable
member needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)) and a type that settings can store
([TSH012](./diagnostics.md#tsh012)); [Tool settings](./tool-settings.md#storable-types) lists those types.
`WithConfigurableSettings` leaves members marked `[Obsolete]` out without a `NotConfigurable` call.

## Agent

| Call | Does |
|---|---|
| `WithName(string name)` | Names the agent, which is otherwise its class name in snake_case. Messages the agent writes carry the name as their author. |
| `WithDescription(string description)` | Says what the agent does, in place of its `<summary>`. A sub-agent or a handoff target needs one of the two ([TSH023](./diagnostics.md#tsh023)). |
| `WithInstructions(string instructions)` | Sets the instructions, the agent's system prompt, for every run. |
| `WithStorageKind(string kind)` | Sets the kind a store keys the agent's `Settings` by, in place of its class name, so renaming the class keeps its stored settings. An empty kind is [TSH039](./diagnostics.md#tsh039). |
| `WithSchemaVersion(int version)` | Sets the schema version of the stored `Settings`, 1 unless raised. Raise it after you rename or remove a configurable member, so you can [migrate older documents](./implementing-a-store.md#migrate-older-settings). A version below 1 is [TSH039](./diagnostics.md#tsh039). |
| `WithChatOptions()` | Returns the [`ChatOptionsBuilder`](#chatoptionsbuilder). |
| `WithRunOptions()` | Returns the [`RunOptionsBuilder`](#runoptionsbuilder). |
| `UseToolset<TToolset>()` | Offers every tool of the toolset. `.Include(t => t.X)` offers only the tools picked, in that order, and `.Exclude(t => t.X)` all but those. One use includes or excludes, never both. For a toolset used twice or a tool picked twice, the analyzer reports [TSH025](./diagnostics.md#tsh025). `.RequiresApproval(t => t.X)` makes calls of a tool the use offers [wait for approval](./tool-approval.md#requiring-approval), and `.Configurable(t => t.X)` lets settings [switch it off](./defining-agents.md#switching-a-tool-off); a pick of a tool the use does not offer is [TSH043](./diagnostics.md#tsh043). |
| `AddTool(Delegate method)` | Declares a tool of the agent's own, backed by a static method of the agent, and returns its [`ToolBuilder`](#toolbuilder). The agent offers it after the tools of its toolsets. See [Agents](./defining-agents.md#add-a-tool-of-the-agent-s-own). |
| `UseAgent<TAgent>()` | Offers another agent as a tool named like it, with one `input` parameter. `.InBackground()` returns at once and delivers the answer later; `.WithResult<T>()` holds the answer to `T`'s JSON. See [Sub-agents](./multi-agent.md#sub-agents). |
| `UseHandoff<TAgent>()` | Offers a tool `handoff_to_{name}` that hands the conversation to the other agent. See [Handoff](./multi-agent.md#handoff). |
| `UseQuestions()` | Offers the built-in `ask_user` tool, with which the model asks the user one to four questions. A call of it ends the run with a `QuestionDecision`, in a run with a conversation. See [Asking the user](./tool-approval.md#asking-the-user). |
| `UseMcpServer(string server)` | Offers the tools of an MCP server that your app's `IMcpToolProvider` connects. `.RequiresApproval()` makes every call to its tools wait for approval, and `.ReadOnly()` lets plan mode offer them. |
| `UseContext<TProvider>()` | Adds what an `IContextProvider` returns to every run. The run's services resolve the provider, or create it when none is registered. |

When your code builds the instructions for each run, override the virtual method
`GetInstructions(IServiceProvider services)` instead of calling `WithInstructions`. Typeshift calls it each time it
resolves a run's options, with the run's services
([Instructions built per run](./defining-agents.md#instructions-built-per-run)).

## ChatOptionsBuilder

`WithChatOptions()` sets the chat options every run starts from. `Configurable(o => o.X)` lets stored settings or a
run change an option, even one the code never sets, and makes it a member of the agent's
[`Settings`](./generated-members.md#settings). An option with "No" in the last column cannot be configurable
([TSH012](./diagnostics.md#tsh012)): it belongs to the run, as the tools do, or settings cannot store its type.

Typeshift has no defaults of its own for chat options. An option that no layer sets stays null in the `ChatOptions`
the client receives, so the provider's default applies:

| Call | Sets | Default | May be configurable |
|---|---|---|---|
| `WithModel(string model)` | `ModelId` alone. An agent that names no provider runs on the client registered for every agent ([Naming a model](./models.md#naming-a-model)) | The model of the client that serves the run | Yes, as `Provider` and `Model` |
| `WithModel(TProviderId provider, string model)` | `ModelId`, and the [provider](./models.md) that serves it, whose type is the agent's [provider id type](./models.md#the-provider-id-type) | The model of the client that serves the run | Yes, as `Provider` and `Model` |
| [`WithInstructions`](#agent) in the agent, or `GetInstructions(services)` overridden | `Instructions` | None | Yes |
| `WithTemperature(float temperature)` | `Temperature` | The provider's | Yes |
| `WithMaxOutputTokens(int tokens)` | `MaxOutputTokens` | The provider's | Yes |
| `WithTopP(float topP)` | `TopP` | The provider's | Yes |
| `WithTopK(int topK)` | `TopK` | The provider's | Yes |
| `WithFrequencyPenalty(float penalty)` | `FrequencyPenalty` | The provider's | Yes |
| `WithPresencePenalty(float penalty)` | `PresencePenalty` | The provider's | Yes |
| `WithSeed(long seed)` | `Seed` | The provider's | Yes |
| `WithStopSequences(params string[] sequences)` | `StopSequences` | The provider's | Yes |
| `WithAllowMultipleToolCalls(bool allow)` | `AllowMultipleToolCalls` | The provider's | Yes |
| `WithReasoning(ReasoningOptions reasoning)` | `Reasoning` | The provider's | Yes |
| `WithResponseFormat(ChatResponseFormat format)` | `ResponseFormat` | The provider's | No |
| `WithToolMode(ChatToolMode mode)` | `ToolMode` | The provider's | No |
| `WithTools(params AITool[] tools)` | Adds tools of no toolset to `Tools`, such as a `HostedWebSearchTool` or a function from `AIFunctionFactory` | None | No |
| `WithReadOnlyTools(params AITool[] tools)` | Adds tools of no toolset to `Tools` as `WithTools` does, and marks them as reading only, so [plan mode](./tool-approval.md#plan-mode) offers them. The provider receives each tool as it was given | None | No |
| `WithAdditionalProperty<TValue>(string key, TValue value)` | A provider-specific option in `AdditionalProperties` | None | No |

Five members of `ChatOptions` have no call. `Instructions` comes from the agent's `WithInstructions`. A run sets
`ConversationId` and `ContinuationToken` itself, and takes the response a model call returns, so it does not set
`AllowBackgroundResponses` either. `RawRepresentationFactory` is set on the client or in a run middleware
([Options of a provider's own](./models.md#options-of-a-provider-s-own)).

## RunOptionsBuilder

`WithRunOptions()` sets how a run goes: its mode, the limits of its tool loop and when its history is compacted. Every
run option may be configurable: `Configurable(r => r.X)` puts it into the agent's `Settings`, like a chat option. A
configurable mode lets those settings name `Auto`, so the analyzer warns about it on an agent with tools that
require approval ([TSH037](./diagnostics.md#tsh037)). `SetModeAsync` on the agent's host switches a conversation's
mode without the pick ([Setting the mode](./tool-approval.md#setting-the-mode)).

| Call | Default | Range | Does |
|---|---|---|---|
| `WithMode(AgentMode mode)` | `Default` | Any mode | Sets the [mode](./tool-approval.md#the-three-modes) runs start in: `Default`, `Plan` or `Auto`. |
| `WithMaxToolIterations(int iterations)` | 40 | 1 or more | Sets the most rounds of tool calls the agent makes in one run. Calls your app [approves](./tool-approval.md#approving-or-rejecting) run in the next run, which counts its own, and so does an agent the run is [handed off](./multi-agent.md#handoff) to. Each run of a [sub-agent](./multi-agent.md#sub-agents) counts its own too, so the limit bounds this agent's calls and not what its sub-agents make. |
| `WithMaxConsecutiveToolErrors(int errors)` | 3 | 0 or more | Sets how many rounds of tool calls in a row may fail before the failing tool's exception ends the run and reaches your code. |
| `WithConcurrentToolCalls(bool concurrent)` | off | On or off | Runs the tool calls in one model response at the same time, sharing the run's scoped services ([Tool calls at the same time](./defining-agents.md#tool-calls-at-the-same-time)). |
| `WithMaxInputTokens(int tokens)` | off | Above 0 | Turns [compaction](./conversations.md#compaction) on at this many input tokens. |
| `WithTargetInputTokens(int tokens)` | half the maximum | Above 0 and below the maximum | Sets how far compaction shrinks the history. |
| `WithSummaryModel(string model)` | the run's model | Not blank | Sets the model that summarizes the oldest turns when clearing old tool results does not shrink the history enough, served by the run's provider. Made configurable, it also adds `SummaryProvider` to `Settings` ([the summary model](./models.md#the-summary-model)). |
| `WithSummaryInstructions(string instructions)` | Typeshift's, in English | Not blank | Sets the instructions the summary model receives, for an app in another language or domain ([Compaction](./conversations.md#compaction)). |

The run options resolve into a `RunOptions` record, with a property for each call named without `With`, such as
`MaxToolIterations`: the property a `Configurable(r => r.X)` pick names, and the one run middleware reads in
`run.RunOptions` ([Middleware](./middleware.md#wrap-every-run)).

No run can go with a value outside its range, so Typeshift refuses one wherever it enters, and each message names the
option:

- In the agent's code, the call throws an `ArgumentOutOfRangeException` from the constructor, or an
  `ArgumentException` for a blank model.
- In settings your app saves, the save throws an `ArgumentException` and stores nothing
  ([What a save refuses](./settings-and-stores.md#what-a-save-refuses)).
- In a run's own settings, or between two layers, such as a stored target above the maximum the code sets, the run
  throws an `InvalidOperationException` before it calls the model or stores an entry in its conversation.

## Related

- [Toolsets](./toolsets.md): declaring tools, and their names, parameters and results.
- [Tool settings](./tool-settings.md): settings records and their configurable members.
- [Agents](./defining-agents.md): declaring an agent and choosing its tools.
- [AgentHost and generated members](./generated-members.md): what the generator writes from these chains.
- [Registration](./registration.md#the-chain): the calls of the `AddTypeshift()` chain.
- [How Typeshift reads your code](./how-typeshift-reads-your-code.md): why the chains follow these rules.
- [Diagnostics](./diagnostics.md): what the analyzer reports when a chain breaks a rule.
