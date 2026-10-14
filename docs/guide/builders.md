# Builders

Toolsets and agents declare themselves in their constructors through the chains below. The generator reads them at
compile time; at runtime they only record values. Every declaration goes directly in the constructor
([TSH014](./diagnostics.md#tsh014)), one chain per statement ([TSH015](./diagnostics.md#tsh015)), and names and
descriptions are constants ([TSH002](./diagnostics.md#tsh002)).

## Toolset

| Call | Does |
|---|---|
| `WithDisplayName(string displayName)` | Names the toolset for people, a tool picker say; read back as `DisplayName`. |
| `AddTool(Delegate method)` | Declares a tool backed by a static method group, `AddTool(SearchTickets)`, and returns its `ToolBuilder`. Lambdas, instance methods and local functions are [TSH003](./diagnostics.md#tsh003). |

A tool's name is the method's in snake_case without an `Async` suffix: `GetTicketAsync` is `get_ticket`. Its
description is the method's `<summary>`, each `<param>` its parameter's. Names are unique across everything the host
sees ([TSH007](./diagnostics.md#tsh007)) and key the tool's stored settings.

### Tool parameters

Every parameter is part of the tool's JSON schema, with its `<param>` doc and default value, except these, which the
call fills and which need no `<param>` doc:

| Type | Receives |
|---|---|
| The record of `WithSettings<T>()` or `WithConfigurableSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The run's services: the scope the host runs in. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

A missing required argument fails the call with an `ArgumentException`, which the tool loop reports to the model; an
absent optional one takes its default. A nullable reference parameter is nullable in the schema. `ref` and `out`
parameters are [TSH004](./diagnostics.md#tsh004).

### Tool results

A tool returns a value, `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`. The result reaches the model as JSON,
serialized through its type's `JsonTypeInfo`.

## ToolBuilder

| Call | Does |
|---|---|
| `WithName(string name)` | Overrides the derived name; lower_snake_case ([TSH008](./diagnostics.md#tsh008)). |
| `WithDescription(string description)` | Overrides the `<summary>`. A tool without either is [TSH009](./diagnostics.md#tsh009). |
| `RequiresApproval()` | Pauses a run that calls the tool until the host approves or rejects the call. |
| `ReadOnly()` | Marks the tool as reading only, without side effects; plan mode offers only these. |
| `WithSettings<TSettings>()` | Gives the tool a settings record whose members stay in code unless `Configurable` names them. Returns `ToolBuilder<TSettings>`. |
| `WithConfigurableSettings<TSettings>()` | Gives the tool a settings record whose settable members may all change unless `NotConfigurable` names them. Returns `ConfigurableSettingsBuilder<TSettings>`. |

The settings come last. `TSettings` is a record with a parameterless constructor, which sets the code defaults
([TSH005](./diagnostics.md#tsh005)), and the tool takes a parameter of its type ([TSH010](./diagnostics.md#tsh010)).

### Configurable members

| Call | On | Does |
|---|---|---|
| `Configurable(s => s.Member)` | `ToolBuilder<TSettings>` | Lets the member change at runtime, through stored settings or a run. |
| `NotConfigurable(s => s.Member)` | `ConfigurableSettingsBuilder<TSettings>` | Keeps the member in code. |

The lambdas never run, and a pick must be `s => s.Member` ([TSH015](./diagnostics.md#tsh015)). Obsolete members stay
out on their own. A configurable member needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013))
and a type settings can store ([TSH012](./diagnostics.md#tsh012)):

- strings, Booleans, numbers, enums (stored by name), `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`,
  `TimeOnly` and `Uri`;
- records and classes with a public parameterless constructor whose settable members are storable, replaced as a
  whole, never merged;
- arrays, lists and sets of storable values;
- dictionaries keyed by `string` or an enum.

A type that contains itself is not storable.

## Agent

| Call | Does |
|---|---|
| `WithName(string name)` | Names the agent; its class name in snake_case without it. Messages it writes carry the name as their author. |
| `WithDescription(string description)` | Says what the agent does; a sub-agent or handoff target needs one ([TSH023](./diagnostics.md#tsh023)). |
| `WithStorageKind(string kind)` | The kind a store keys the agent's `Settings` by, in place of its class name. |
| `WithSettingsVersion(int version)` | The schema version of its stored `Settings`, 1 unless raised. |
| `WithChatOptions()` | Returns the [`ChatOptionsBuilder`](#chatoptionsbuilder). |
| `WithRunOptions()` | Returns the [`RunOptionsBuilder`](#runoptionsbuilder). |
| `UseToolset<TToolset>()` | Offers every tool of the toolset. `.Include(t => t.X)` offers only those picked, in that order; `.Exclude(t => t.X)` all but those. One use includes or excludes, never both; use each toolset once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). |
| `UseAgent<TAgent>()` | Offers another agent as a tool named like it, with one `input` parameter. `.InBackground()` returns at once and delivers the answer later. |
| `UseHandoff<TAgent>()` | Offers a tool `handoff_to_{name}` that hands the conversation to the other agent. |
| `UseMcpServer(string server)` | Offers the tools of an MCP server the host's `IMcpToolProvider` connects. `.RequiresApproval()` asks before every call of its tools, `.ReadOnly()` lets plan mode offer them. |
| `UseContext<TProvider>()` | Adds what an `IContextProvider` returns to every run. |

`GetInstructions()`, a virtual method, returns the instructions; it runs whenever a run's options are resolved. An
agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)).

## ChatOptionsBuilder

Sets the chat options every run starts from. `Configurable(o => o.X)` lets a run or stored settings change an option,
whether the code sets it or not; the configurable ones become members of the agent's
[`Settings`](./generated-members.md#settings), the others are [TSH012](./diagnostics.md#tsh012).

| Call | Sets | May be configurable |
|---|---|---|
| `WithModel(string model)` | `ModelId`, as the `IChatClientProvider` names models | Yes, as `Model` |
| `GetInstructions()` | `Instructions` | Yes |
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
| `WithTools(params AITool[] tools)` | Adds to `Tools`: tools of no toolset, a `HostedWebSearchTool` say | No |
| `WithAdditionalProperty<TValue>(string key, TValue value)` | A provider-specific option in `AdditionalProperties` | No |

## RunOptionsBuilder

Sets how a run's tool loop goes. `Configurable(r => r.X)` puts an option into the agent's `Settings`, like a chat
option.

| Call | Default | Does |
|---|---|---|
| `WithMode(AgentMode mode)` | `Default` | The [mode](./modes.md) runs start in. |
| `WithMaxToolIterations(int iterations)` | 40 | The most rounds of tool calls one run makes. |
| `WithMaxConsecutiveToolErrors(int errors)` | 3 | The most failed tool calls in a row before the run ends. |
| `WithConcurrentToolCalls(bool concurrent)` | off | Runs the tool calls of one model response at the same time. |
| `WithMaxInputTokens(int tokens)` | off | Turns [compaction](./long-running-agents.md#compaction) on at this many input tokens. |
| `WithTargetInputTokens(int tokens)` | half the maximum | How far compaction shrinks the history. |
| `WithSummaryModel(string model)` | none | Summarizes what clearing tool results did not free; without one, the oldest turns are dropped. |

### AgentMode

| Mode | Offers | Tools that require approval |
|---|---|---|
| `Default` | every tool | ask the host, unless a rule or a standing approval decides |
| `Plan` | read-only tools and `exit_plan_mode` | offered only if also `ReadOnly()`; they still ask |
| `Auto` | every tool | run without asking, unless a rule rejects the call |
