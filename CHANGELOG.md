# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [SemVer](https://semver.org/).

## [Unreleased]

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
