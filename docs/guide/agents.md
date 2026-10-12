# Agents

An agent bundles what a model needs for one job: its chat options, the tools it may call and the settings those
tools run with. Like a toolset, it is declared in its constructor and the generator reads it at compile time.

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `AgentDefinition`:

snippet: agent

- **`WithName`** is optional; without it the agent goes by its class name. The name labels the execution context of
  the agent's conversations. It need not be unique, since nothing is stored by name.
- **`WithDescription`** says what the agent does, for people choosing among agents.
- **`WithChatOptions()`** starts a chain that sets the chat options every run starts from: `WithModel`,
  `WithTemperature`, `WithMaxOutputTokens` and one `With...` per other option. `WithModel` names the model the agent
  runs on unless its settings say otherwise. Tools added with `WithTools`, a hosted web search for example, are
  offered before the toolsets' tools.
- **`GetInstructions()`** is a virtual method returning the agent's instructions, its system prompt. Override it,
  with a raw string literal for anything longer than a line. It runs whenever a run's options are resolved, so the
  instructions may also be computed.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain tools after it, `UseToolset<T>().SearchTickets().GetTicket()`,
to offer only those: the generator adds one method per tool to the selection, named after the tool, so
`search_tickets` is `SearchTickets()`. A tool of another toolset has no method there, so selecting it does not
compile. Keep the chain in one expression; the generator reads it from the syntax. Offering one tool twice throws an
`InvalidOperationException` when the agent's tools are first read.

## Configurable chat options

`Configurable(o => o.Temperature)` in the `WithChatOptions()` chain marks a chat option that may change at runtime,
whether the code sets it or not. Like the settings picks, it is read by the generator only. Configurable options must
have a type the settings can store ([AGT014](./diagnostics.md#agt014)).

## Tool settings

`WithToolSettings()` sets the settings the agent's tools run with, over the tool's code defaults:
`WithToolSettings().SearchTickets().WithLimit(5)`. The generator writes it per agent, with one method per offered
tool that has settings and one `With...` per settable member of the tool's settings record, configurable or not. A
tool the agent does not offer has no method, so setting its settings does not compile. A tool the agent sets this way
no longer takes its stored defaults; see [the layers](./tool-settings.md#the-layers).

## The generated settings

For every agent, the generator writes a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may
change at runtime:

| Member | From |
|---|---|
| `Model` | Always there; overrides `ChatOptions.ModelId`. |
| One property per configurable chat option | `Instructions`, `Temperature`, `MaxOutputTokens` above. |
| One property per tool with configurable settings | `SearchTickets`, `SearchArticles`: the tools' [overrides](./tool-settings.md#the-generated-overrides). |

Every value is nullable, and null keeps what the layer below sets. The record is the agent's settings contract:
store it [per id](./persistence.md#agent-settings), pass it [for one run](./running-agents.md#run-settings), or bind
a settings UI or an API to it. It serializes itself to JSON, so it needs no serializer setup.

## The runtime

`AddAgentic()` registers each agent as a singleton together with an `AgentRuntime<TAgent>`, the object a host runs
it through; see [running agents](./running-agents.md). Every runtime is also registered as `AgentRuntime`, so a host
can list all agents.
