---
title: Agent host
description: Every member of AgentHost<TAgent> with its signature, and what it loads, stores and returns.
---

# Agent host

`AgentHost<TAgent>` runs one agent. This page lists its members, the library's and the ones the generator writes for
each agent; [Running agents](./running-agents.md) shows them in use.

## The host's scope

`AddTypeshift()` registers a host per agent, as transient. So a host runs with the services of the scope it was resolved
from: tools, the chat client provider, middleware, context providers, approval rules and the MCP tool provider all
resolve from that scope.

Every member that loads or saves needs a store. Without one, it throws an `InvalidOperationException` that names
`WithStore<T>()`.

## Library members

### `Agent`

`TAgent Agent { get; }`: the agent as its code declares it, a singleton. `Agent.Name` and `Agent.Description` read what
`WithName` and `WithDescription` declared.

### `CreateChatClientAsync`

`Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)`

Creates the client of the [raw path](./running-agents.md#the-raw-path), for options that `ResolveOptionsAsync` returned.

- It asks the `IChatClientProvider` for the options' model, and wraps that client in the run's pipeline: the tool loop,
  with the agent's run options.
- Calling the client with those options runs and stores like `RunAsync`. `ChatOptions.ConversationId` names the
  conversation, as a string of the id type.
- Disposing the client leaves the provider's client alone.

It throws an `InvalidOperationException` when the options name no model, or when no provider is registered.

## Generated members

The generator writes these members for each agent, in an extension block next to it; see
[Generated members](./generated-members.md#agent-hostextensions). In the signatures, `TId` is the build's
[id type](./registration.md#the-id-type) and `Settings` the agent's
[generated `Settings` record](./generated-members.md#settings). Every member takes a `CancellationToken ct = default`
last.

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

Runs the agent on one message: a `string`, or a `ChatMessage`, which can carry images or approval answers.

- **Layers** the agent's code, the settings stored under `settingsId`, the mode the conversation's last run ended in,
  then `settings`; see [Settings layering](./settings-and-stores.md#settings-layering).
- **Stores** the run in the conversation as it goes, when you pass a `conversationId`; see
  [What a run stores](#what-a-run-stores). Without one, it stores nothing, offers no handoffs, and throws when a tool
  asks for approval.
- **Returns** the model's messages, and the tool calls and results between them. Each message carries the answering
  agent's name as `AuthorName`.

`RunAsync<TResult>` asks the model for JSON of `TResult`'s schema, and returns it typed in `Result`. Under
[Native AOT](./production.md#native-aot), `TResult` needs JSON metadata.

### `RunStreamingAsync`

Layers and stores like `RunAsync`, but streams the updates. Tool calls run between them. With `WithRealtimeTracking()`,
the updates are also published to `ConversationTracker.WatchAsync`.

### `ResumeAsync` and `ResumeStreamingAsync`

Continue the conversation's interrupted run from its last stored step, without new input. They layer like `RunAsync` and
store the continuation in the same conversation. A call that was in flight when the run stopped is left out, so the
model decides again. They throw an `InvalidOperationException` when the conversation has no interrupted run.

### `ResolveOptionsAsync`

Layers the agent's code, the settings stored under `settingsId`, then `settings`, and returns the run's `ChatOptions`.
They hold the model, the instructions, the configurable options and the tools. The tools come in this order: the
toolsets' tools with their layered settings, the sub-agents, the tools of `WithTools`, then the MCP servers' tools. It
stores nothing.

### `LoadSettingsAsync`

Loads the `Settings` stored under an id, with their revision. When none are stored, it returns empty settings at
revision 0. It layers nothing: the record holds only what was stored.

### `SaveSettingsAsync`

Saves `Settings` under an id if the stored revision is still `revision`, and returns the new revision. It throws a
`StoreConflictException` when another save came first.

### `ConfigureAsync`

Loads the settings, applies `configure`, and saves them with the loaded revision. On a conflict, it loads and applies
again until its save wins. It returns what it saved.

## What a run stores

A run with a conversation id stores its input, each model response, each tool result and an entry that ends the run.
[Conversations](./conversations.md#what-is-stored) lists what is stored when a run fails, is cancelled or is cut off,
and [compaction](./conversations.md#compaction) what a compacted run stores.

## Related

- [Running agents](./running-agents.md): resolving a host and running an agent.
- [Conversations](./conversations.md): storing, resuming and steering a conversation.
- [Settings and stores](./settings-and-stores.md#agent-settings): loading, saving and configuring an agent's settings.
- [Settings layering](./settings-and-stores.md#settings-layering): the order in which settings apply.
- [Generated members](./generated-members.md): where the typed members come from.
