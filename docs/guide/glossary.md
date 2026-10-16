---
title: Glossary
description: The words these docs use and what exactly they mean in Typeshift.
---

# Glossary

Typeshift reuses Microsoft.Extensions.AI's words wherever it can: a run returns a `ChatResponse`, a model is served by
an `IChatClient`. The words below are Typeshift's own, or have a narrower meaning here.

| Term | Meaning |
|---|---|
| **Agent** | A `partial` class deriving from `Agent` that bundles a model's chat options, instructions, tools and their settings for one job. See [Agents](./defining-agents.md). |
| **Agent host** | `AgentHost<TAgent>`, which runs one agent with the services of the scope it was resolved from. See [Running agents](./running-agents.md). |
| **Approval request** | What a run that needs approval ends with: Microsoft.Extensions.AI's `ToolApprovalRequestContent`, answered in the conversation's next run. See [Tool approval](./tool-approval.md). |
| **Approval rule** | An `IToolApprovalRule` that approves, rejects or abstains on a call before the host is asked. |
| **Chat client provider** | The host's `IChatClientProvider`, which returns the `IChatClient` for a model name. |
| **Code defaults** | The values a settings record's parameterless constructor sets: the lowest [layer](./settings-layering.md). |
| **Compaction** | Shrinking the history a model call sees once it nears a token limit. See [Long-running agents](./long-running-agents.md#compaction). |
| **Configurable** | A settings member or option the code allows to change at runtime, through stored settings or a run. Everything else stays what the code says. |
| **Context provider** | An `IContextProvider` that adds instructions, messages or tools to every run of the agents that use it. |
| **Conversation** | The entries a run stores under a conversation id: inputs, model responses, tool results and run endings. See [Conversations](./conversations.md). |
| **Declaration** | The chain of calls in a toolset's or agent's constructor, such as `AddTool(...).ReadOnly()`. The generator reads it at compile time; at runtime it only records values. |
| **Diagnostic** | A `TSHnnn` error or warning the analyzer reports where the generator cannot read a declaration. See [Diagnostics](./diagnostics.md). |
| **Generated member** | A member the source generator writes into or next to your toolsets and agents. See [Generated members](./generated-members.md). |
| **Handoff** | A tool, `handoff_to_{name}`, that hands the conversation to another agent. See [Multiple agents](./multi-agent.md#handoff). |
| **Id type** | The type of every settings id and conversation id in a build: `Guid` unless `TypeshiftIdType` names another. |
| **Kind** | The stable name a store keeps a document under, such as an agent's class name. |
| **Layer** | One source of settings: the code defaults, the agent's code, the stored settings or the run. A higher layer overrides a lower one. |
| **Middleware** | An `IRunMiddleware` around every run or an `IToolMiddleware` around every tool call. |
| **Mode** | `Default`, `Plan` or `Auto`: what a run offers the model, and what happens to tools that require approval. See [Plan and auto modes](./modes.md). |
| **Module** | The registration the generator writes for each assembly that declares toolsets or agents. `AddTypeshift()` registers the modules of the project and its references. |
| **Overrides** | The generated `{Tool}Overrides` record: a tool's configurable settings, each nullable. Null keeps the layer below. |
| **Raw path** | Resolving a run's options and creating its `IChatClient` yourself, for a host that needs the client. See [Running agents](./running-agents.md#the-raw-path). |
| **Revision** | The number a store raises on every save of a document, so a save fails when another came first. |
| **Run** | One call of `RunAsync`, `RunStreamingAsync` or `ResumeAsync`: the model calls and tool calls until the model answers. |
| **Run options** | How a run's tool loop goes: its mode, its limits and compaction. Set with `WithRunOptions()`. |
| **Settings** | An agent's generated `Settings` record: exactly the values that may change at runtime. Stored under a settings id, or passed for one run. |
| **Settings id** | The id the host stores an agent's `Settings` under: one per customer, team or user, as the host decides. |
| **Settings record** | The record a tool receives its settings in, declared with `WithSettings<T>()`. The model never sees it. |
| **Stamp** | An attribute the generator writes so that agents in other assemblies can read a toolset's tools. |
| **Standing approval** | An approval for every later call of a tool in a conversation, given with `ApproveForConversation()`. |
| **Steering** | Input a user sends while a run works, handed to that run with `TrySteer`. |
| **Store** | The host's one `ITypeshiftStore`, which keeps settings and conversations. See [Settings and stores](./settings-and-stores.md). |
| **Sub-agent** | Another agent offered as a tool with `UseAgent<T>()`. It works in a context of its own and answers with text. |
| **Tool** | A static method a toolset declares with `AddTool`, offered to the model as a generated `AIFunction`. |
| **Tool list** | The generated `{Toolset}ToolList`, one property per tool, that `Include` and `Exclude` pick from. |
| **Toolset** | A `partial` class deriving from `Toolset` that declares the tools of one domain. See [Toolsets](./toolsets.md). |

## Related

- [Getting started](./getting-started.md#how-the-pieces-fit): how the pieces fit together.
- [Writing style](../contributing/writing.md): how the docs use these words.
