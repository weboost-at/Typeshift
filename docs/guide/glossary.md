---
title: Glossary
description: The words these docs use and what exactly they mean in Typeshift.
---

# Glossary

Typeshift reuses Microsoft.Extensions.AI's words wherever it can: a run returns a `ChatResponse`, and an
`IChatClient` serves a model. The terms below are Typeshift's own, or mean something narrower here.

## Words you need

Every app meets these words. They name agents and toolsets, runs, settings and conversations.

| Term | Meaning |
|---|---|
| <a id="agent"></a>**Agent** | A `partial` class deriving from `Agent` that bundles chat options, instructions, tools and their settings for one job. See [Agents](./defining-agents.md). |
| <a id="agent-host"></a><a id="host"></a>**Agent host** | `AgentHost<TAgent>`, also written "the host", which runs one agent with the services of its scope: the one it was resolved from, or the one the runtime created for it. The application that uses Typeshift is "your app", never the host. See [Using an agent](./using-an-agent.md). |
| <a id="agents-own-settings"></a>**Agent's own settings** | The `Settings` an agent keeps under no id, which every run of the agent applies. A host without a settings id loads and saves them, through an `IAgentSettingsStore`. See [An agent's own settings](./settings-and-stores.md#an-agent-s-own-settings). |
| <a id="approval-rule"></a>**Approval rule** | An `IToolApprovalRule` that approves, rejects or abstains on a tool call before your app is asked. See [Approval rules](./tool-approval.md#approval-rules). |
| <a id="built-in-tool"></a>**Built-in tool** | A tool a run offers itself: `ask_user`, `exit_plan_mode` and each handoff's `handoff_to_{name}`, whose names `BuiltInTools` holds. No tool of yours may have one of these names ([TSH035](./diagnostics.md#tsh035)). See [Names and descriptions](./toolsets.md#names-and-descriptions). |
| <a id="catalog"></a>**Catalog** | The agents and toolsets the runtime lists, `runtime.Agents` and `runtime.Toolsets`, and the hosts it creates by an agent's kind. See [The catalog](./registration.md#the-catalog). |
| <a id="chat-client-provider"></a>**Chat client provider** | An `IChatClientProvider<T>` your app may register with `WithChatClientProvider<TProvider>()`, which returns the `IChatClient` for a provider of type `T` and a model. Without one for its agent's provider id type, a run that names a provider takes the `IChatClient` registered under it. See [Your own provider](./models.md#your-own-provider). |
| <a id="compaction"></a>**Compaction** | Shrinking the history a model call sees once it nears a token limit. See [Compaction](./conversations.md#compaction). |
| <a id="configurable"></a>**Configurable** | A settings member or an option that stored settings or a run may change, marked in code with `Configurable(...)` or `WithConfigurableSettings<T>()`. Everything else keeps the value the code sets. See [Configurable members](./tool-settings.md#configurable-members). |
| <a id="context-provider"></a>**Context provider** | An `IContextProvider` that adds instructions, messages or tools to every run of the agents that use it. See [Context providers](./middleware.md#context-providers). |
| <a id="conversation"></a>**Conversation** | The entries runs store under a conversation id, which `WithConversationId` gives a host: inputs, model responses, tool results and run endings. See [Conversations](./conversations.md). |
| <a id="decision"></a>**Decision** | What a run asks your app before it goes on: an `ApprovalDecision` for a call of a tool that requires approval, a `PlanDecision` for a plan, a `QuestionDecision` for questions to the user. It is stored with the conversation and answered by its id through `AnswerAsync`. See [Approval and modes](./tool-approval.md#how-approval-works). |
| <a id="default-client"></a>**Default client** | The `IChatClient` registered for every agent, with `services.AddChatClient(client)` or `TypeshiftRuntime.Create(client)`. It is the default provider unless `WithDefaultProvider(key)` names a keyed client in its place. See [Registering clients](./models.md#registering-clients). |
| <a id="default-provider"></a>**Default provider** | The provider that serves every run that names none: the keyed client `WithDefaultProvider(key)` names, or else the default client. It never serves a run whose provider nothing is registered for. See [The default provider](./models.md#the-default-provider). |
| <a id="diagnostic"></a>**Diagnostic** | A `TSHnnn` error or warning the analyzer reports for a mistake in a toolset, an agent or the setup. See [Diagnostics](./diagnostics.md). |
| <a id="handoff"></a>**Handoff** | A tool, `handoff_to_{name}`, declared with `UseHandoff<T>()`, that hands the conversation to another agent. See [Sub-agents and handoffs](./multi-agent.md#handoff). |
| <a id="layer"></a>**Layer** | One source of settings: the code defaults, the agent's code, the stored settings or the run. A higher layer overrides a lower one, and a conversation's mode sits between the stored settings and the run. See [Settings layering](./settings-and-stores.md#settings-layering). |
| <a id="mcp-server"></a>**MCP server** | A server that offers tools over the Model Context Protocol. An agent names one with `UseMcpServer("...")`, and your app's `IMcpToolProvider` connects it and returns its tools. See [MCP servers](./mcp.md). |
| <a id="middleware"></a>**Middleware** | An `IRunMiddleware` around every run, or an `IToolMiddleware` around every tool call. See [Middleware](./middleware.md#middleware). |
| <a id="mode"></a>**Mode** | `Default`, `Plan` or `Auto`, which decides what a run offers the model and what happens to tools that require approval. A conversation keeps the mode a run's settings, an approved plan or `SetModeAsync` named. See [The three modes](./tool-approval.md#the-three-modes). |
| <a id="provider"></a>**Provider** | Who serves a run's model calls, such as one vendor account or one endpoint, named by its key, such as `"openai"`: by the call site's `host.WithProvider(key)`, the agent's settings or its code's `WithProvider(key)`, or else the default provider. It is chosen apart from the model. See [Models and providers](./models.md). |
| <a id="question"></a>**Question** | What the model asks the user through the built-in `ask_user` tool, which an agent offers with `UseQuestions()`: a text, a short header and two to four options. Your app answers with the labels the user picked or the user's own words. See [Asking the user](./tool-approval.md#asking-the-user). |
| <a id="read-only-tool"></a>**Read-only tool** | A tool marked `ReadOnly()`, which only reads and changes nothing. Plan mode offers only these. See [Plan mode](./tool-approval.md#plan-mode). |
| <a id="run"></a>**Run** | One call of `RunAsync`, `RunAsync<T>`, `RunStreamingAsync`, `RunStreamingAsync<T>`, `ResumeAsync`, `ResumeStreamingAsync`, an `AnswerAsync` or `AnswerStreamingAsync` that continues, or the raw path's client: the model calls and tool calls until the model answers or asks your app. See [How a run works](./how-a-run-works.md). |
| <a id="runtime"></a>**Runtime** | `ITypeshiftRuntime`, the root of Typeshift in your app, which gives you a host with a scope of its own. `TypeshiftRuntime.Create` builds one without a container, and `AddTypeshift()` registers one. See [The runtime](./registration.md#the-runtime). |
| <a id="settings"></a>**Settings** | An agent's generated `Settings` record, with exactly the values that may change at runtime. Stored as the agent's own, stored under a settings id, or passed for one run. See [Agent settings](./settings-and-stores.md#agent-settings). |
| <a id="settings-id"></a>**Settings id** | The id your app stores an agent's `Settings` under, such as one per customer, team or user, and gives a host with `WithSettingsId`. See [Settings under an id](./settings-and-stores.md#settings-under-an-id-such-as-per-customer). |
| <a id="standing-approval"></a>**Standing approval** | An approval for every later call of a tool in a conversation, given with `ApproveForConversation()`, listed by `GetDecisionsAsync()` and withdrawn with `RevokeStandingApprovalAsync`. See [Approving for the conversation](./tool-approval.md#approving-for-the-conversation). |
| <a id="steering"></a>**Steering** | Input a user sends while a run works, which you hand to that run with `ConversationTracker<TId>.TrySteer`. See [Steering](./conversations.md#steering). |
| <a id="store"></a>**Store** | What your app registers with `WithStore<T>()` to keep settings (`ISettingsStore<TId>`), conversations (`IConversationStore<TId>`), their decisions (`IDecisionStore<TId>`) or all three (`ITypeshiftStore<TId>`). See [Register your store](./implementing-a-store.md#register-your-store). |
| <a id="stored-settings"></a>**Stored settings** | The `Settings` a store keeps: an agent's own, and those under a settings id. A run applies them over the agent's code and under the run's settings. See [Settings layering](./settings-and-stores.md#settings-layering). |
| <a id="sub-agent"></a>**Sub-agent** | Another agent offered as a tool with `UseAgent<T>()`. It works in a context of its own and answers with text. See [Sub-agents](./multi-agent.md#sub-agents). |
| <a id="tool"></a>**Tool** | A static method that a toolset or an agent declares with `AddTool`, offered to the model as a generated `AIFunction`. See [Declaring tools](./toolsets.md#declaring-tools). |
| <a id="tool-exception"></a>**Tool exception** | A `ToolException`, which a tool throws to tell the model why a call failed. The model reads its message, and of any other exception only that the call failed. See [When a tool fails](./toolsets.md#when-a-tool-fails). |
| <a id="tool-settings"></a>**Tool settings** | The record a tool receives its settings in, also called its settings record, declared with `WithSettings<T>()` or `WithConfigurableSettings<T>()`. The model never sees it. See [Tool settings](./tool-settings.md). |
| <a id="toolset"></a>**Toolset** | A `partial` class deriving from `Toolset` that declares the tools of one domain. See [Toolsets](./toolsets.md). |
| <a id="tracker"></a>**Tracker** | `ConversationTracker<TId>`, which knows the runs going on in this container for conversations of one id type. Your app lists them through it, steers a run, and follows a run's live updates, the updates the run publishes while it works. See [Live runs](./conversations.md#live-runs). |
| <a id="typed-run"></a>**Typed run** | `RunAsync<T>` or `RunStreamingAsync<T>`: a run that asks the model for JSON of `T`'s schema and returns it typed, in `Result`. Also called structured output. See [Structured output](./running-agents.md#structured-output). |

## Words for store authors and for how Typeshift works

You meet these words when you write a store, read generated code, or look up how Typeshift reads a
declaration.

| Term | Meaning |
|---|---|
| <a id="code-defaults"></a>**Code defaults** | The values a settings record's parameterless constructor sets, the lowest [layer](./settings-and-stores.md#settings-layering). |
| <a id="declaration"></a>**Declaration** | The chain of calls in a toolset's or agent's constructor, such as `AddTool(...).ReadOnly()`. The generator reads it at compile time, and at runtime it only records values. See [How Typeshift reads your code](./how-typeshift-reads-your-code.md). |
| <a id="generated-member"></a>**Generated member** | A member the source generator writes into or next to your toolsets and agents. See [AgentHost and generated members](./generated-members.md). |
| <a id="id-type"></a>**Id type** | The type of a settings id or a conversation id, such as `int` or `Guid`, which picks the store that keeps it. One app may use several. See [Ids of your own types](./settings-and-stores.md#ids-of-your-own-types). |
| <a id="kind"></a>**Kind** | The stable name a store keeps a document under. An agent's settings kind is its class name, or the name `WithStorageKind("...")` sets. See [Key settings by id and kind](./implementing-a-store.md#key-settings-by-id-and-kind). |
| <a id="module"></a>**Module** | The registration the generator writes for each assembly with toolsets, agents or documented types: its toolsets, agents, JSON contexts and XML docs. `AddTypeshift()` registers the modules of the project and its references. See [What `AddTypeshift()` registers](./registration.md#what-addtypeshift-registers). |
| <a id="overrides"></a>**Overrides** | The generated `{Tool}Overrides` record, a tool's configurable settings with each one nullable. Null keeps the layer below. See [The generated overrides](./tool-settings.md#the-generated-overrides). |
| <a id="provider-id-type"></a>**Provider id type** | The type of an agent's providers: that of the provider its `WithProvider(provider)` names, such as `string` or an enum, and `string` for an agent that names no provider in code. One app may use several. See [Models and providers](./models.md#the-provider-id-type). |
| <a id="raw-path"></a>**Raw path** | Resolving a run's options and creating its `IChatClient` yourself, for when your app needs the client. See [The raw path](./running-agents.md#the-raw-path). |
| <a id="run-options"></a>**Run options** | How a run's tool loop goes, with its mode, its limits and compaction. Set with `WithRunOptions()`. See [Builders](./builders.md#runoptionsbuilder). |
| <a id="schema-version"></a>**Schema version** | The number in a stored document's JSON, `"$schemaVersion"`, that names the shape of the record that wrote it: 1 unless the agent's `WithSchemaVersion(n)` raises it. Reading a newer one fails. See [The stored JSON](./settings-and-stores.md#the-stored-json). |
| <a id="stamp"></a>**Stamp** | A hidden attribute the generator writes so that other assemblies can read a toolset's tools or a project's module. See [Generated members](./generated-members.md#names-and-hidden-members). |
| <a id="tool-list"></a>**Tool list** | `ToolList<TToolset>`, the type `Include` and `Exclude` pick tools from. A generated extension block gives it one property per tool of the toolset. See [The tool list](./generated-members.md#the-tool-list). |
| <a id="tool-names"></a>**Tool names** | The generated `ToolNames` class of a toolset, or of an agent with tools of its own: one constant per tool, holding its name, for code that names a tool outside a declaration. See [`ToolNames`](./generated-members.md#toolnames). |
| <a id="version"></a>**Version** | The number that marks a stored document's current state; each save gives the document a new one. A save passes the version it loaded, so it fails when another save came first. Not the schema version. See [Save with a version](./implementing-a-store.md#save-with-a-version). |

## Related

- [What is Typeshift?](./what-is-typeshift.md#how-the-pieces-fit): how the pieces fit together.
- [Writing the docs](../contributing/writing.md): for contributors, how these docs use the terms above.
