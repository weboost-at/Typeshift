# Agents

An agent bundles what a model needs for one job: its chat options, the tools it may call and the settings those
tools run with. Like a toolset, it is declared in its constructor and the generator reads it at compile time.

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `AgentDefinition`:

snippet: agent

- **`WithName`** is optional; without it the agent goes by its class name. The name labels the execution context of
  the agent's conversations. It need not be unique, since nothing is stored by name.
- **`WithDescription`** says what the agent does, for people choosing among agents.
- **`WithChatOptions`** sets the `ChatOptions` every run starts from. `ModelId` is the model the agent runs on unless
  its settings say otherwise. Tools set here, a hosted web search for example, are offered before the toolsets'
  tools.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. `UseToolset<T>(tools => ...)` offers the tools the lambda selects,
through one method per tool that the generator adds to the selection, named after the tool:
`search_tickets` is `SearchTickets()`. A tool of another toolset has no method there, so selecting it does not
compile. Offering one tool twice throws an `InvalidOperationException` when the agent is created.

## Configurable chat options

The picker of `WithChatOptions` marks the chat options that may change at runtime. Like the settings picks, it is
read by the generator only. Configurable options must have a type the settings can store
([AGT014](./diagnostics.md#agt014)).

## Tool settings

`WithToolSettings(...)` sets the settings the agent's tools run with, as a whole object, over the tool's code
defaults. The generator writes one overload per settings type the agent's selected tools read, so passing settings
no tool of the agent reads does not compile. The settings apply to every tool of the agent that reads that type.

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
