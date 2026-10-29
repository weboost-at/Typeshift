---
title: Glossary
description: The words these docs use and what exactly they mean in Typeshift.
---

# Glossary

Typeshift reuses Microsoft.Extensions.AI's words wherever it can: a run returns a `ChatResponse`, and an
`IChatClient` serves a model. The terms below are Typeshift's own, or mean something narrower here.

| Term | Meaning |
|---|---|
| **Agent** | A `partial` class deriving from `Agent` that bundles chat options, instructions, tools and their settings for one job. See [Agents](./defining-agents.md). |
| **Agent host** | `AgentHost<TAgent>`, which runs one agent with the services of the scope it was resolved from. See [Running agents](./running-agents.md). |
| **Approval request** | The `ToolApprovalRequestContent` a run ends with when it needs approval. You answer it in the conversation's next run. See [Approval and modes](./tool-approval.md). |
| **Approval rule** | An `IToolApprovalRule` that approves, rejects or abstains on a tool call before your app is asked. |
| **Chat client provider** | The `IChatClientProvider` your app registers with `WithChatClientProvider<T>()`, which returns the `IChatClient` for a provider and a model. |
| **Code defaults** | The values a settings record's parameterless constructor sets, the lowest [layer](./settings-and-stores.md#settings-layering). |
| **Compaction** | Shrinking the history a model call sees once it nears a token limit. See [Compaction](./conversations.md#compaction). |
| **Configurable** | A settings member or an option that stored settings or a run may change, marked in code with `Configurable(...)` or `WithConfigurableSettings<T>()`. Everything else keeps the value the code sets. |
| **Context provider** | An `IContextProvider` that adds instructions, messages or tools to every run of the agents that use it. |
| **Conversation** | The entries a run stores under a conversation id: inputs, model responses, tool results and run endings. See [Conversations](./conversations.md). |
| **Declaration** | The chain of calls in a toolset's or agent's constructor, such as `AddTool(...).ReadOnly()`. The generator reads it at compile time, and at runtime it only records values. |
| **Diagnostic** | A `TSHnnn` error or warning the analyzer reports for a mistake in a toolset, an agent or the setup. See [Diagnostics](./diagnostics.md). |
| **Generated member** | A member the source generator writes into or next to your toolsets and agents. See [Generated members](./generated-members.md). |
| **Handoff** | A tool, `handoff_to_{name}`, declared with `UseHandoff<T>()`, that hands the conversation to another agent. See [Multiple agents](./multi-agent.md#handoff). |
| **Host** | Short for agent host, `AgentHost<TAgent>`. The application that uses Typeshift is "your app". |
| **Id type** | The type of every settings id and conversation id in a build: `Guid` unless `TypeshiftIdType` names another. |
| **Kind** | The stable name a store keeps a document under. An agent's settings kind is its class name, or the name `WithStorageKind("...")` sets. |
| **Layer** | One source of settings: the code defaults, the agent's code, the stored settings or the run. A higher layer overrides a lower one, and a conversation's mode sits between the stored settings and the run. |
| **Middleware** | An `IRunMiddleware` around every run, or an `IToolMiddleware` around every tool call. |
| **Mode** | `Default`, `Plan` or `Auto`, which decides what a run offers the model and what happens to tools that require approval. See [The three modes](./tool-approval.md#the-three-modes). |
| **Module** | The registration the generator writes for each assembly that declares toolsets or agents. `AddTypeshift()` registers the modules of the project and its references. |
| **Overrides** | The generated `{Tool}Overrides` record, a tool's configurable settings with each one nullable. Null keeps the layer below. |
| **Provider id type** | The type of every provider id in a build, such as the provider in `WithModel(provider, model)`: `string` unless `TypeshiftProviderIdType` names another. See [Models and providers](./models.md#the-provider-id-type). |
| **Raw path** | Resolving a run's options and creating its `IChatClient` yourself, for when your app needs the client. See [Running agents](./running-agents.md#the-raw-path). |
| **Run** | One call of `RunAsync`, `RunAsync<T>`, `RunStreamingAsync`, `ResumeAsync`, `ResumeStreamingAsync` or the raw path's client: the model calls and tool calls until the model answers. |
| **Run options** | How a run's tool loop goes, with its mode, its limits and compaction. Set with `WithRunOptions()`. |
| **Schema version** | The number in a stored document's JSON, `"$schemaVersion"`, that names the shape of the record that wrote it: 1 unless the agent's `WithSchemaVersion(n)` raises it. Reading a newer one fails. See [The stored JSON](./settings-and-stores.md#the-stored-json). |
| **Settings** | An agent's generated `Settings` record, with exactly the values that may change at runtime. Stored under a settings id, or passed for one run. |
| **Settings id** | The id your app stores an agent's `Settings` under, such as one per customer, team or user. |
| **Settings record** | The record a tool receives its settings in, declared with `WithSettings<T>()` or `WithConfigurableSettings<T>()`. The model never sees it. |
| **Stamp** | A hidden attribute the generator writes so that other assemblies can read a toolset's tools or a project's module. |
| **Standing approval** | An approval for every later call of a tool in a conversation, given with `ApproveForConversation()`. |
| **Steering** | Input a user sends while a run works, which you hand to that run with `ConversationTracker.TrySteer`. See [Steering](./conversations.md#steering). |
| **Store** | What your app registers with `WithStore<T>()` to keep settings (`ISettingsStore`), conversations (`IConversationStore`) or both (`ITypeshiftStore`). See [Settings and stores](./settings-and-stores.md). |
| **Sub-agent** | Another agent offered as a tool with `UseAgent<T>()`. It works in a context of its own and answers with text. |
| **Tool** | A static method a toolset declares with `AddTool`, offered to the model as a generated `AIFunction`. |
| **Tool list** | `ToolList<TToolset>`, the type `Include` and `Exclude` pick tools from. The generated `{Toolset}ToolList` gives it one property per tool. |
| **Toolset** | A `partial` class deriving from `Toolset` that declares the tools of one domain. See [Toolsets](./toolsets.md). |
| **Version** | The number that marks a stored document's current state; each save gives the document a new one. A save passes the version it loaded, so it fails when another save came first. Not the schema version. See [Versions](./implementing-a-store.md#versions). |

## Related

- [Getting started](./getting-started.md#how-the-pieces-fit): how the pieces fit together.
- [Writing style](../contributing/writing.md): for contributors, how these docs use the terms above.
