# Agent host

`AgentHost<TAgent>` runs one agent. `AddTypeshift()` registers it per agent as transient, so it runs with the services
of the scope it was resolved from: tools, the chat client provider, middleware, context providers, approval rules and
the MCP tool provider resolve from that scope.

In the signatures below, `TId` is the build's [id type](./registration.md#the-id-type) and `Settings` the agent's
[generated `Settings` record](./generated-members.md#settings). Every member that loads or saves throws an
`InvalidOperationException` naming `WithStore<T>()` when no store is registered.

## Library members

### `Agent`

`TAgent Agent { get; }`: the agent as its code declares it, a singleton. `Agent.Name` and `Agent.Description` read
what `WithName` and `WithDescription` declared.

### `CreateChatClientAsync`

`Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)`

The raw path's client for options `ResolveOptionsAsync` returned: it asks the `IChatClientProvider` for the options'
model and wraps the client in the run's pipeline, the tool loop with the agent's run options included. Calling it
with those options runs and stores like `RunAsync`, with `ChatOptions.ConversationId` naming the conversation as a
string of the id type. Disposing it leaves the provider's client alone.

Throws an `InvalidOperationException` when the options name no model or no provider is registered.

## Generated members

The generator writes these per agent, in an extension block next to it; see
[generated members](./generated-members.md#agent-hostextensions). All take a `CancellationToken ct = default` last.

| Member | Returns |
|---|---|
| `RunAsync(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync(ChatMessage input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync<TResult>(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse<TResult>>` |
| `RunStreamingAsync(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `RunStreamingAsync(ChatMessage input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `ResumeAsync(TId conversationId, TId? settingsId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `ResumeStreamingAsync(TId conversationId, TId? settingsId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `ResolveOptionsAsync(TId? settingsId = null, Settings? settings = null)` | `Task<ChatOptions>` |
| `LoadSettingsAsync(TId settingsId)` | `Task<Stored<Settings>>` |
| `SaveSettingsAsync(TId settingsId, Settings settings, long revision)` | `Task<long>` |
| `ConfigureAsync(TId settingsId, Func<Settings, Settings> configure)` | `Task<Settings>` |

### `RunAsync`

Runs the agent on one message, a `string` or a `ChatMessage` that carries images or approval answers.

- **Layers** the agent's code, the settings stored under `settingsId`, the mode the conversation's last run ended in,
  then `settings`; see [settings layering](./settings-layering.md).
- **Stores**, with a `conversationId`, the run in the conversation as it goes; see [what a run stores](#what-a-run-stores).
  Without one it stores nothing, offers no handoffs, and throws when a tool asks for approval.
- **Returns** the model's messages and the tool calls and results between them, each with the answering agent's name
  as `AuthorName`.

`RunAsync<TResult>` asks the model for JSON of `TResult`'s schema and returns it typed in `Result`. Under Native AOT,
`TResult` needs JSON metadata.

### `RunStreamingAsync`

Layers and stores like `RunAsync`, streaming the updates; tool calls run between them. With
`WithRealtimeTracking()`, the updates are also published to `ConversationTracker.WatchAsync`.

### `ResumeAsync` and `ResumeStreamingAsync`

Continue the conversation's interrupted run from its last stored step, without new input. They layer like `RunAsync`
and store the continuation in the same conversation. A call that was in flight when the run stopped is left out, so
the model decides again. Throw an `InvalidOperationException` when the conversation has no interrupted run.

### `ResolveOptionsAsync`

Layers the agent's code, the settings stored under `settingsId`, then `settings`, and returns the run's
`ChatOptions`: the model, the instructions, the configurable options, and the tools, those of the toolsets with their
layered settings first, then the sub-agents, the tools of `WithTools`, and the MCP servers' tools. Stores nothing.

### `LoadSettingsAsync`

Loads the `Settings` stored under an id with their revision; empty settings at revision 0 when none are stored. Layers
nothing: the record holds only what was stored.

### `SaveSettingsAsync`

Saves `Settings` under an id if the stored revision is still `revision`, and returns the new one. Throws a
`StoreConflictException` when another save came first.

### `ConfigureAsync`

Loads the settings, applies `configure` and saves them with the loaded revision. On a conflict it loads and applies
again, until its save wins, and returns what it saved.

## What a run stores

A run with a conversation id appends to the conversation as it goes: its input, each model response with its model
and token usage, each tool result, and an entry that ends the run and holds its mode.

- A failed run ends with an `ErrorContent` entry, then the exception surfaces.
- A stream that fails, is cancelled or is left early ends with the updates that arrived.
- A cancelled run is stored too; storing does not observe the run's token.
- A store that fails to save is logged and recorded on the run's span; the run still returns its answer.
- A run cut off by a crash has no ending entry; `ResumeAsync` continues it.
- Compaction stores what it left, the summary and the messages after it, as an entry later runs start from.

On resume, tool calls that never got a result and error messages are left out, since providers reject them.
