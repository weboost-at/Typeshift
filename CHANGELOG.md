# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

### Added

- `TypeshiftProviderIdType` sets the type of provider ids for the build, `string` by default, and the generator writes
  the `IChatClientProvider` alias for it. A string, a number, a `Guid` or an enum works.
- TSH030 reports a provider of another type than the build's at `WithModel` and at `WithChatClientProvider<T>()`.
- `ITypeshiftStore.LoadConversationSinceCompactionAsync` returns a conversation's last summary and the entries after it.
  Its default loads the whole conversation; a store overrides it to leave the entries before the summary unread.
- A tool may return `IAsyncEnumerable<T>`: the call reads it to its end, and the model receives its items as one array.

### Changed

- An agent names its model by a provider and a model: `WithModel(provider, model)`. `GetChatClientAsync` receives
  both, and a request carries the model alone as `ChatOptions.ModelId`, so composite names and the client wrappers
  that undid them go. A configurable model brings `Provider` into the agent's `Settings`, a configurable summary model
  `SummaryProvider`.
- Compaction always summarizes: without a summary model the run's model does it, and `WithSummaryModel(model)` names
  a model the run's provider serves. It no longer drops the oldest turns.
- TSH029 is "Id type cannot be used" and covers `TypeshiftProviderIdType` too.
- TSH021 covers `TypeshiftProviderIdType` too, so a referenced project with agents built with another provider id
  type fails the build instead of its runs. Module stamps carry the provider id type, at stamp version 2.
- A run loads its conversation from the last summary on. A summary keeps the conversation's mode, its standing
  approvals and the background calls still working; a run of a conversation an earlier version summarized loads it
  whole.

### Fixed

- The metrics `gen_ai.client.token.usage` and `gen_ai.client.operation.duration` no longer reset after every run:
  the provider's client is instrumented once and kept so as long as it lives, instead of a source and a meter per
  run. Return the same client instance per model from `IChatClientProvider` where you can.
- Steering input waiting when the model hands the conversation to another agent is stored for that agent, so the
  conversation's next run goes to it instead of back to the agent that handed off.
- An approval still finds its stored request when run middleware adds or removes messages ahead of the input.
- TSH007 reports a tool name two referenced projects share, on the project that references both.
- Merging the rounds of a run no longer adds the later rounds' usage into the first response's `Usage`.
- A tool parameter of a recursive type gets `$ref`s that resolve: they point into the parameter's schema instead of
  the function's root.
- The merged text updates `ConversationTracker.WatchAsync` publishes keep the first update's ids, so a watcher
  groups them into messages as the run's caller does.
- A background sub-agent that its run's end cancelled no longer faults an unobserved task.
- A public agent with a configurable model and a less accessible provider id type is TSH006 on the pick, instead of
  CS0053 in the generated `Settings`.
- The file tools compare a path as the operating system opens it, so on Windows a last segment's trailing dots and
  spaces cannot lead above the root.
- TSH023 and TSH024 check a sub-agent or handoff target of a referenced project too: its module stamp carries the
  agent's name, whether it has a description and the tools it offers that require approval, at stamp version 3.
- Options the raw path builds itself run with the agent's run options through the whole pipeline.
- A run of an agent with handoffs loads its conversation once instead of twice.
- TSH022 reports two agents of one kind in two referenced projects too, on the project that references both.
- `run_command` keeps what a command wrote just before its timeout: it reads the output to its end once the timeout
  killed the command, and for a second past the timeout while a process left behind holds it open, where it stopped
  reading at once.
- `ConfigureAsync` gives up after ten conflicts in a row and throws the last `StoreConflictException`, where a store
  that conflicted every time kept it going until its token was cancelled.
- A streaming run that compacted keeps its summary when steering input or a background answer starts another pass,
  where the pass lost it and could summarize the same turns again.

## [0.1.0-alpha.2] - 2026-10-16

### Added

- The properties of the types tools take are described by their XML docs: the package brings
  Weboost.SchemaAnnotations and adds the class it collects docs for to every project with
  `GenerateDocumentationFile`. `TypeshiftSchemaAnnotations=false` turns it off. A project that marked its own
  `[SchemaAnnotations]` class deletes it.
- `ConversationTracker.WatchRunsAsync(ct)` yields the runs going on, then each run as it starts and ends
  (`LiveRun.Ended`), so a list of running conversations needs no polling.

### Changed

- A failed tool call reaches the model with its exception's message, so the model can correct the call.

## [0.1.0-alpha.1] - 2026-10-16

The library was rebuilt from the API its owner approved; nothing of the unreleased first alpha shipped.

### Added

- Toolsets: static methods become `AIFunction`s through a source generator, with schemas from their parameters and
  XML docs and reflection-free argument binding. Tool names derive from the method (`CloseTicketAsync` is
  `close_ticket`); `WithName`, `WithDescription`, `ReadOnly()` and `RequiresApproval()` refine them.
- Tool settings: a settings record passed to the tool as a parameter the model never sees, with configurable members
  as an allow list (`WithSettings<T>().Configurable(s => s.Limit)`) or a deny list
  (`WithConfigurableSettings<T>().NotConfigurable(s => s.Timeout)`), and a generated `{Tool}Overrides` record.
- Agents: chat options, instructions, toolset uses with `Include` and `Exclude`, run options and
  `Configure{Tool}()`, read at compile time into a generated `Settings` record of what may change at runtime.
- `AddTypeshift()`: one intercepted call registers the toolsets and agents of the project and its references, an
  `AgentHost<T>` per agent and the assemblies' JSON contexts. Runs (`RunAsync`, `RunAsync<T>`, `RunStreamingAsync`,
  the raw `ResolveOptionsAsync` and `CreateChatClientAsync`) use the services of the host's scope and return
  Microsoft.Extensions.AI's types.
- Stored settings: `ITypeshiftStore<TId>` with revisions, `InMemoryTypeshiftStore`, `WithStore<T>()`, and per agent
  `LoadSettingsAsync`, `SaveSettingsAsync` and `ConfigureAsync`; runs take a settings id. The id type is the
  `TypeshiftIdType` MSBuild property; the generator writes `ITypeshiftStore` and `InMemoryTypeshiftStore` aliases for
  it. Settings serialize themselves member by member, `System.Text.Json` included, with kinds and versions.
- Conversations: runs with a conversation id resume it and store every step as it happens, so `ResumeAsync`
  continues a run a crash cut off. `ConversationTracker` lists the runs going on, steers them with `TrySteer`, and
  with `WithRealtimeTracking()` streams their updates to watchers.
- Tool approval: a run that calls a tool marked `RequiresApproval()` ends with Microsoft.Extensions.AI's approval
  requests, and the conversation's next run carries the answers; the stored request is the call that runs.
  `ApproveForConversation()` approves a tool for the rest of the conversation, and `IToolApprovalRule`s
  (`WithApprovalRule<T>()`) approve or reject calls before the host is asked.
- Plan and auto modes: `AgentMode` is a run option. Plan mode offers the read-only tools and `exit_plan_mode`, whose
  plan the host approves with `ApprovePlan(mode)`; auto mode runs what requires approval unless a rule rejects it. A
  conversation keeps its mode.
- Middleware and context: `IRunMiddleware` around every run, streaming or not, `IToolMiddleware` around every tool
  call (`WithRunMiddleware<T>()`, `WithToolMiddleware<T>()`), and `IContextProvider`s an agent names with
  `UseContext<T>()`, which add instructions, messages and tools to each run.
- Compaction: `WithMaxInputTokens`, `WithTargetInputTokens` and `WithSummaryModel` shrink a long run's history
  before each model call, clearing old tool results, then summarizing or dropping the oldest turns; the summary is
  stored, and later runs start from it.
- OpenTelemetry: the `Typeshift` source and meter trace each run as `invoke_agent`, with `chat` and `execute_tool`
  spans inside, after the GenAI semantic conventions.
- Multiple agents: `UseAgent<T>()` offers an agent as a tool that answers in a fresh context, in the foreground or
  `InBackground()`; `UseHandoff<T>()` hands the conversation to another agent, which answers its later runs. The
  analyzer checks both (TSH023, TSH024, TSH026).
- MCP: `UseMcpServer(name)` offers a server's tools, which the host's `IMcpToolProvider` connects
  (`WithMcpToolProvider<T>()`), with `RequiresApproval()` and `ReadOnly()`.
- Built-in toolsets in `Typeshift.Toolsets`: `TodoToolset` (`write_todos`), `FileSystemToolset` within an
  `IFileSystemRoot`, `ShellToolset` (`run_command`, behind approval) and `WebToolset` over the host's `IWebSearch`
  and `IWebFetch`.
- Native AOT: generated paths never reflect; the analyzer reports a tool type without JSON metadata in an AOT
  project (TSH027).
- An analyzer reports every Typeshift diagnostic at real source locations, so `#pragma` and `.editorconfig` reach
  them; a suppressor drops CS1573 for tool parameters the model never sees.
