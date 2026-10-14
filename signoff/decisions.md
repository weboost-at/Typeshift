# Decisions (draft for the sign-off)

The fresh log of the rewrite. Each entry: context, decision, alternatives, where it will live. **Status** says whether
the owner decided it (in the brief or in this session), whether it carries a v0 decision over (v0's number in
brackets), or whether it is **proposed** and waits for the sign-off. After the sign-off this file replaces
`docs/contributing/decisions.md`, and code cites entries as `// Dnn: ...`.

## Registration and the generator

### D1. `AddTypeshift()` exists only as an intercepted call

- **Status:** carried (v0 D1).
- **Decision:** the library's `AddTypeshift()` throws, saying what to check; the generator intercepts every call in a
  project. It returns `TypeshiftBuilder`, the one registration chain. A library that calls `AddTypeshift()` inside its
  own `AddX()` registers only its own modules; the docs say so.
- **Alternatives:** a generated method per assembly, ambiguous (CS0121) wherever `InternalsVisibleTo` reaches.
- **Where:** `DependencyInjection/`, `Emitting/InterceptorEmitter.cs`.

### D2. An analyzer reports the diagnostics; the generator only writes code

- **Status:** proposed (the brief's retrospective).
- **Context:** v0 reported TSH diagnostics without syntax trees, so `#pragma` and `.editorconfig` could not reach
  them, and locations in the models broke caching.
- **Decision:** a `DiagnosticAnalyzer` in the same package reports every TSH diagnostic at real locations; the parser
  takes a report callback, a no-op in the generator. Compilation-wide checks (unique names, references, the id type)
  are compilation-end actions. The analyzer sees generated code, so it checks picks and the store type semantically.
  A `DiagnosticSuppressor` silences CS1573 for tool parameters the model never sees.
- **Alternatives:** the generator reporting with real locations, which keeps syntax nodes out of cached models only
  with v0's plumbing.
- **Where:** `src/Typeshift.Generators/Analysis/`.

### D3. Discovery stays attribute-free, narrowed by syntax

- **Status:** proposed.
- **Context:** v0 re-bound every class with a base list on each edit (about 170 ms per keystroke for 50 toolsets).
- **Decision:** the syntax predicate takes classes whose base list names `Toolset` or `Agent` (any qualification);
  the semantic check confirms. No marker attribute.
- **Alternatives:** a marker attribute with `ForAttributeWithMetadataName`, which reruns only changed files but adds
  ceremony to every declaration.
- **Where:** `Parsing/Discovery.cs`.

### D4. Every declaration shape the generator cannot read is diagnosed

- **Status:** proposed (the brief's retrospective, v0 D29 kept).
- **Decision:** `Include(t => t.X)` stays, picked from the generated `ToolList<T>` (v0 D29): a foreign tool does not
  compile. The generator reads the picks from syntax, since the list's properties are its own output; the analyzer
  reports every other shape: a chain split over variables, a pick that is not `t => t.X` (TSH015), a declaration
  outside the constructor body (TSH014).
- **Alternatives:** method groups (`Include(TicketToolset.SearchTickets)`), which bind semantically but take a
  `Delegate` and fail only through a diagnostic.
- **Where:** `Parsing/AgentParser.cs`, `Analysis/`.

### D5. Settings and chat-option picks are typed lambdas that never run

- **Status:** carried (v0 D5), typed.
- **Decision:** `Configurable<TMember>(Func<TSettings, TMember>)`: no `object`. The generator reads the picks; the
  configurable set exists once, in the generated records.
- **Where:** `Toolset.cs`, `Agent.cs`, `Parsing/ConstructorChains.cs`.

### D6. The generated names

- **Status:** carried (v0 D2), extended.
- **Decision:** per toolset a `{Tool}Overrides` record per tool with configurable settings and a `{Toolset}ToolList`
  extension block; per agent the nested `Settings` record, a private `Configure{Tool}()` per offered tool with
  settings and an `{Agent}HostExtensions` extension block; per assembly a module; per project the id aliases. Nested
  classes join their containers' names with `_`; keywords get `@`. A clash with a user member is TSH018.
- **Where:** `Emitting/Names.cs`.

### D7. Stamps carry the generated names and a version

- **Status:** proposed.
- **Context:** v0's stamps carried tool names only, so an agent rebuilt `{Tool}Overrides` names with its own
  generator version.
- **Decision:** tool stamps name the overrides type and the list property, and every module stamp has a stamp
  version; an unknown version is TSH020.
- **Where:** `Modules/`, `Parsing/ReferenceReader.cs`.

### D8. The generator targets Roslyn 5

- **Status:** proposed.
- **Decision:** consumers need C# 14 for the extension blocks, so the generator uses Roslyn 5 APIs; v0's 4.14 pin and
  its trap go.

### D9. Every project that declares toolsets or agents references the package

- **Status:** carried (v0 D9). TSH020 catches a reference without it.

### D10. The hidden public surface generated code calls

- **Status:** carried (v0 D35).
- **Decision:** what only generated code calls is public and `[EditorBrowsable(Never)]`, in `Typeshift.Generated`.

### D11. The id type is an MSBuild property

- **Status:** decided by the owner in this session.
- **Context:** ids are typed end to end and the library is compiled before the host chooses its id type.
- **Decision:** `<TypeshiftIdType>int</TypeshiftIdType>`, set once in `Directory.Build.props`, `Guid` by default.
  The library's id-carrying types are generic (`ITypeshiftStore<TId>`); the generator writes per project
  `global using` aliases (`ITypeshiftStore`, `InMemoryTypeshiftStore`, `ConversationTracker`, `LiveRun`) and
  generates typed members with the concrete type. A reference built with another id type is TSH021; a store of
  another is TSH028.
- **Alternatives:** `AddTypeshift().WithIdType<int>()`, known only in the registering project, with generic run
  members checked at runtime; a generated interface per project, ambiguous under `InternalsVisibleTo`.
- **Where:** `buildTransitive/Typeshift.props`, `Emitting/AliasEmitter.cs`.

### D12. Typed host members are generated extension blocks

- **Status:** carried (v0 D4).
- **Decision:** `extension(AgentHost<TAgent> host)` per agent, in the agent's assembly, with `RunAsync`,
  `RunAsync<T>`, `RunStreamingAsync`, `ResumeAsync`, `ResumeStreamingAsync`, `LoadSettingsAsync`,
  `SaveSettingsAsync`, `ConfigureAsync` and `ResolveOptionsAsync`. They call hidden library methods, so the logic lives and is tested in the library.

## Declarations

### D13. Tool names derive from the method

- **Status:** proposed (an open question of the brief).
- **Decision:** `SearchTickets` is `search_tickets`; an `Async` suffix is dropped, so `CloseTicketAsync` is
  `close_ticket`. `WithName` overrides. The name keys stored settings, so renaming a method renames the tool: the docs
  say to pin the old name with `WithName` first. TSH007 also compares the generated property names.
- **Alternatives:** keeping `Async` (`close_ticket_async`), which leaks a C# convention to the model; a stored key
  separate from the name, a second identity.
- **Where:** `Parsing/ToolsetParser.cs`.

### D14. Descriptions come from XML docs

- **Status:** carried (v0), `WithDescription` overrides; parameters the model never sees need no `<param>` (D2's
  suppressor).

### D15. Tools receive services through `IServiceProvider`

- **Status:** decided by the owner in this session.
- **Context:** the brief asked to investigate injecting resolved services as parameters.
- **Decision:** tools stay static and take `IServiceProvider`, the run's services. No injection.
- **Alternatives:** `[FromServices]` parameters (one attribute, TSH for an interface parameter without it); inference
  by type (interfaces are services), leaky for collection interfaces and concrete services such as a `DbContext`.

### D16. A tool receives its settings as a parameter

- **Status:** carried (v0 D40). The settings, `IServiceProvider`, `CancellationToken` and `AIFunctionArguments` are
  bound by type and left out of the schema; `ref` and `out` are TSH004.

### D17. One flat chain per declaration

- **Status:** carried (v0 D41), decided in the brief. No builder inside a lambda anywhere: toolsets, agents,
  registration. Metadata goes through `With...` calls, capabilities through `Use...` calls.

### D18. Configurable members are an allow list unless `WithConfigurableSettings` says otherwise

- **Status:** carried (v0 D43).

### D19. Configurable types widen to what the generator can serialize

- **Status:** proposed (the brief's retrospective).
- **Decision:** besides v0's scalars, enums and string lists: `DateOnly`, `TimeOnly`, `Uri`, records and classes of
  storable settable members (replaced as a whole, never merged), arrays, lists and sets of storable values,
  dictionaries keyed by `string` or an enum. The generator writes their serialization member by member, as for the
  records themselves; anything else is TSH012. Recursive types are TSH012.
- **Where:** `Parsing/Storage.cs`, `Emitting/StorableEmitter.cs`.

### D20. `Configure{Tool}()` stays

- **Status:** carried (v0 D30), decided in the brief.
- **Decision:** a generated private method per offered tool with settings, returning a builder with a `With...` per
  settable member. No better shape fits the one-chain rule: a picker (`Configure(t => t.X)`) still needs a generated
  builder per tool, one step longer.

### D21. Instructions come from a virtual method

- **Status:** carried (v0 D42), decided in the brief.

### D22. Agent names are optional and derive from the class

- **Status:** carried (v0 D31), changed.
- **Decision:** without `WithName`, an agent goes by its class name in snake_case, like a tool, so it can be a tool or
  a handoff target. Names need not be unique; nothing is stored by name (D39).

### D23. No agent inheritance, no composition block in v1

- **Status:** decided by the owner in this session.
- **Decision:** agents and toolsets derive from `Agent` and `Toolset` directly (TSH017). Shared declarations come
  from static methods and constants. A preset class folded into agents (`UsePreset<T>()`) was considered and
  deferred.

### D24. Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules

- **Status:** proposed; standing approvals and rules decided by the owner in this session.
- **Decision:** `RequiresApproval()` wraps the tool in `ApprovalRequiredAIFunction`; `FunctionInvokingChatClient`
  ends the run with `ToolApprovalRequestContent`s, listed by `response.ApprovalRequests`. The host answers with
  `CreateResponse(approved)` in the next run of the conversation; the stored request is the one that runs. Calls in
  the same response that need no approval run anyway. A run that asks without a conversation throws, naming the tool.
  `request.ApproveForConversation()` approves the tool for the rest of the conversation; the approval is stored with
  it, so it holds across runs and processes. `IToolApprovalRule`s (`WithApprovalRule<T>()`, scoped) decide calls
  before the host is asked, seeing the exact arguments: `Approve`, `Reject(reason)` (wins over every approval) or
  `Abstain`, which the mode resolves (D52). A layer above `FunctionInvokingChatClient` answers the requests rules and
  standing approvals decide and continues the loop, so only the rest reach the host. Approvals match tools by
  name, which is unique per agent (TSH007, and a runtime check for MCP tools).
- **Alternatives:** an approval callback the run awaits, which holds a run open across requests; a Typeshift approval
  type, a second vocabulary.

## Runtime

### D25. A run uses the services of the scope its host was resolved from

- **Status:** proposed; withdraws v0 D11 (a scope per tool call).
- **Context:** v0 gave each tool call a fresh scope of the root container, so request-scoped services (the current
  customer) never reached tools, context or the chat client provider (audit finding 2).
- **Decision:** `AgentHost<TAgent>` is transient and keeps the provider it was resolved from. Tools, context
  providers, middleware, the MCP tool provider and the chat client provider resolve from it. Agents and toolsets stay
  singletons, the store too.
- **Alternatives:** a run-context tool parameter carrying the typed id, which built-in toolsets compiled before the
  host cannot type; per-call scopes, which lose the request.
- **Where:** `AgentHost.cs`.

### D26. Runs return Microsoft.Extensions.AI's types

- **Status:** proposed.
- **Decision:** `RunAsync` returns `ChatResponse`, `RunAsync<T>` `ChatResponse<T>`, `RunStreamingAsync`
  `ChatResponseUpdate`s; messages carry the answering agent as `AuthorName`. Inputs are a `string` or a
  `ChatMessage` (for images and approvals). The raw path (`ResolveOptionsAsync`, `CreateChatClientAsync`, the client)
  stays; options it resolved remember their settings id, so the conversation id is the only string.
- **Alternatives:** an own `AgentResponse`, a type to learn for what `ChatResponse` holds.

### D27. Four settings layers

- **Status:** decided in the brief. Code defaults, the agent's code, its stored `Settings`, the run. Stored tool
  defaults (`ToolDefaults`) are gone, so resolving a run's settings is one document.

### D28. The model is a setting

- **Status:** carried (v0 D34), decided in the brief; **question for the owner**.
- **Decision:** every `Settings` has `Model`, which overrides `WithModel`.
- **Note:** a settings form a customer may edit then lets them pick any model the provider knows. The alternative is
  to make it opt-in like every other option, `Configurable(o => o.ModelId)`.

### D29. The chat client pipeline

- **Status:** proposed.
- **Decision:** outermost first: conversation tracking (resume, run boundaries, live updates), run middleware,
  context providers, the `invoke_agent` span, approval (standing approvals and rules), `FunctionInvokingChatClient`
  (handoff, run options, tool middleware as its `FunctionInvoker`), checkpoints (each step appended before compaction
  sees it; steering input and background answers injected here), compaction, `OpenTelemetryChatClient`, the
  provider's client behind a wrapper that never disposes it.
  Checkpoints and compaction sit inside the tool loop, so they act before every model call.
- **Where:** `AgentHost.cs`.

### D30. Middleware: one interface around runs, one around tool calls

- **Status:** proposed.
- **Decision:** `IRunMiddleware.InvokeAsync(run, next, ct)` serves streaming and non-streaming runs alike: it may
  change messages and options, read `run.Response` after `next`, or set it instead of calling `next`.
  `IToolMiddleware` wraps `FunctionInvocationContext`. Registered in the chain, in order, for every agent; a
  middleware filters by `run.Agent`.
- **Alternatives:** MAF's separate run and streaming delegates (streaming falls back to non-streaming when one is
  missing); per-agent middleware in the agent's chain, a second place.

### D31. Context providers

- **Status:** proposed.
- **Decision:** `UseContext<T>()`; `IContextProvider.GetContextAsync(run, ct)` returns an `AgentContext` of
  instructions, messages and tools, appended to the run's. Called once per run after resume; its messages are never
  stored. No after-run hook: run middleware covers it.

### D32. Run options, with compaction inside the tool loop

- **Status:** proposed; long-horizon scope decided by the owner in this session.
- **Context:** the first draft compacted only when a conversation resumed, so one long run outgrew its context, and
  the tool loop's limits were Microsoft.Extensions.AI's constants (40 iterations).
- **Decision:** `WithRunOptions()`: `WithMaxToolIterations`, `WithMaxConsecutiveToolErrors`,
  `WithConcurrentToolCalls`, `WithMaxInputTokens`, `WithTargetInputTokens`, `WithSummaryModel`, and `Configurable`
  over the `RunOptions` record, so a run option joins `Settings` like a chat option. Before each model call, when the
  last call's reported input tokens reached the maximum, the history shrinks to the target: oldest tool results
  cleared first (calls kept), then the oldest turns summarized (or dropped without a summary model). Instructions,
  the latest `write_todos` call and call/result pairs stay; the summary is stored as an entry later runs start from.
  Without reported usage, tokens are estimated from the text.
- **Alternatives:** MAF's strategy pipeline and trigger combinators, more concepts for the same two steps; a
  tokenizer dependency; message-count triggers, which ignore that one tool result may outweigh a hundred messages; a
  separate `WithCompaction()` chain next to the limits.

### D33. Sub-agents, in the foreground or the background

- **Status:** proposed; background sub-agents decided by the owner in this session.
- **Decision:** `UseAgent<T>()` offers a tool named like the agent, with its description and one `input` string. A
  call runs it in a fresh context with the same settings id, the caller's mode, standing approvals and rules, and no
  conversation of its own, and returns its text. `.InBackground()` makes the tool return at once; the answer reaches
  the calling run as a message before a later model call, and the run does not end while sub-agents work. A
  sub-agent cannot ask the host: in the default mode its calls that need approval and that nothing approves are
  rejected (TSH024 warns). Circles are TSH026.
- **Alternatives:** passing a sub-agent's approval requests up to the caller's host, which pauses a parent run on a
  child's call and needs the child's state stored to resume (possible later); MAF's six background-task tools
  (start, wait, results, continue, clear), where one tool and an injected answer do the job; storing each sub-agent
  run as a conversation of its own, which needs ids the library may not create.

### D34. Handoff continues with the agent handed to

- **Status:** decided by the owner in this session.
- **Decision:** `UseHandoff<T>()` adds a declaration-only tool `handoff_to_{name}`. Calling it ends the current
  agent's loop; the target answers in the same run with the whole conversation. The conversation's last entry names
  the agent that answers its next run, if it is reachable through the host agent's handoffs. Handoff needs a
  conversation; run settings apply to the host's agent only.

### D35. MCP servers through a host provider

- **Status:** proposed.
- **Decision:** `UseMcpServer(name)` with `.RequiresApproval()`; the host's `IMcpToolProvider` returns the server's
  tools, connected with the MCP client of its choice. Typeshift takes no MCP dependency. Provider-side servers use
  `HostedMcpServerTool` in `WithTools`.

### D36. OpenTelemetry is always on

- **Status:** proposed.
- **Decision:** source and meter `Typeshift`, GenAI conventions (`invoke_agent`, `chat`, `execute_tool`, operation
  duration, token usage); message content only with `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`. No API.

### D37. The built-in toolsets

- **Status:** proposed (scope from the brief).
- **Decision:** in `Typeshift.Toolsets`, declared with Typeshift's own toolset API:
  - `TodoToolset`: `write_todos` replaces the whole list; the list lives in the conversation (the latest call).
  - `FileSystemToolset`: six tools within an `IFileSystemRoot` the host registers per scope; absolute paths, `..`
    and escaping links refused; the three reading tools `ReadOnly()`, `delete_file` behind approval.
  - `ShellToolset`: `run_command` in the root, always behind approval; timeout and output cap as settings.
  - `WebToolset`: `web_search` and `web_fetch` over `IWebSearch` and `IWebFetch`, no provider shipped.
- **Alternatives:** MAF's five todo tools with state in the session (more tokens, a store per session); file access
  in a store abstraction.

## Storage

### D38. One store of documents and conversations

- **Status:** proposed; updates v0 D25.
- **Decision:** `ITypeshiftStore<TId>` (aliased `ITypeshiftStore`): `LoadAsync` and `SaveAsync` of documents with
  revisions, `LoadConversationAsync` and `AppendConversationAsync` of `ConversationEntry`s keyed by the conversation
  id. Documents stay a closed set (each agent's `Settings`, `ConversationEntry`), so store generators stay
  possible.
- **Alternatives:** v0's load and save only, which rewrote a whole conversation per turn; a generic append for any
  kind, a concept with one user.

### D39. Kinds are stable names

- **Status:** proposed.
- **Decision:** `IStorable.Kind`: the agent's class name for its `Settings`, `conversation_entry` for conversation entries.
  Moving an agent to another namespace keeps its data; renaming its class does not. Two agents of one class name are
  TSH022.
- **Alternatives:** the agent's name, which would need unique names (out of scope); an explicit `WithStorageKind`.

### D40. A schema version per document

- **Status:** proposed.
- **Decision:** `IStorable.Version`, 1 for `Settings` until `WithSettingsVersion(n)` raises it. The store keeps it
  next to the JSON and passes it to `Deserialize`, which refuses a newer version; migrating older documents is the
  store's job.
- **Alternatives:** a version hashed from the record's shape (automatic, but no store can map it to a migration); a
  migration hook on the agent; an analyzer against a shipped list of stored members (later).

### D41. Optimistic concurrency

- **Status:** proposed.
- **Decision:** `SaveAsync` takes the revision the caller loaded, 0 for none, and throws `StoreConflictException`
  when the stored one differs. `ConfigureAsync` reloads and reapplies until it wins (each conflict means another
  save won). `SaveSettingsAsync` takes the revision from `LoadSettingsAsync`.

### D42. Settings ids and conversation ids are separate; conversations grow by appending

- **Status:** proposed (audit findings 1 and 5, the owner's review of the naming).
- **Context:** the first draft keyed conversations by the run's `id`, its settings id, as their owner. The owner
  found `id: customer` confusing: one id named whose settings apply and who owns a conversation.
- **Decision:** runs take `settingsId` (which stored `Settings` apply) and `conversationId` (which conversation
  resumes), independent of each other; either may be left out. What a settings id stands for (a customer, a team, a
  user, a profile) is the host's choice. A conversation is keyed by its id alone: the host authorizes it before
  passing it, as any id from a request, and the guide says so. Runs append entries (D50); no locks, no second
  read.
- **Alternatives:** an explicit `ownerId` on conversation runs, a third id whose authorization the host still owns;
  keying conversations by settings id, which ties two unrelated choices of the host.

### D43. The store is the persistence switch

- **Status:** carried (v0 D26). Without `WithStore<T>()`, runs without a settings or conversation id work; everything
  that loads or saves throws, naming `WithStore<T>()`.

### D44. Settings records serialize themselves

- **Status:** carried (v0 D27), extended.
- **Decision:** member by member through hidden helpers, no reflection; camelCase, tools under `"tools"` by name,
  nulls left out. The generator also writes a `JsonConverter` on the record, so `System.Text.Json` (an ASP.NET
  endpoint) writes the stored shape too.

### D45. Tool JSON: contexts from the modules, reflection outside AOT

- **Status:** proposed; keeps v0 D44.
- **Decision:** modules register the `JsonSerializerContext`s that list their tools' types (v0 D44). Outside Native
  AOT, Microsoft.Extensions.AI's reflection-based defaults cover the rest, so tools need no context; under AOT
  (`PublishAot` or `IsAotCompatible`), a type no context lists is TSH027.

## Conversations

### D46. Tracking, carried and fixed

- **Status:** carried (v0 D18), with the audit's fixes.
- **Decision:** tracking wraps the tool loop and marks where a run begins and ends; checkpoints inside it store
  each step (D50). The provider never receives the
  conversation id. On resume, unanswered calls and error messages are left out. A failed save is logged and traced,
  and the answer returns. The agent's name goes on `AuthorName`, never into `AdditionalProperties`.

### D47. Live updates are a feed per conversation

- **Status:** proposed (the brief: options for intervals and limits).
- **Decision:** `WithRealtimeTracking(options)` turns publishing on; `ConversationTracker.WatchAsync(conversation)`
  reads a bounded feed that drops its oldest updates when the watcher falls behind. A run never waits
  for a watcher. `PublishInterval` (100 ms) and `MaxPendingUpdates` (256) are options. `Runs` lists the runs of
  conversations going on in this process.
- **Alternatives:** v0's process-wide events, awaited up to 5 s per model call.

### D50. A conversation is stored step by step and resumes after a crash

- **Status:** proposed; long-horizon scope decided by the owner in this session.
- **Context:** the first draft stored a run when it ended, so a run that died after an hour of work lost all of it.
- **Decision:** a conversation is a log of `ConversationEntry`s: a run's input, each model response (with model and
  usage), each tool result, compaction summaries; the run's last entry has `EndsRun`. Checkpoints append each step
  the moment it exists, tool results before the next model call. A run without an ending entry was interrupted:
  `ResumeAsync(conversationId)` (and `ResumeStreamingAsync`) continues it without new input, leaving out a call that
  was in flight; a new run on the conversation also starts from its steps. One run per conversation at a time in a
  process; across processes it is the host's to ensure.
- **Alternatives:** one entry per run, rewritten at each step (a whole-run write per model call); re-running the
  interrupted tool call, unsafe for side effects; a `RunAsync` with a null input in place of `ResumeAsync`.
- **Where:** `Conversations/`, the checkpoint layer of D29.

### D51. Steering a run going on

- **Status:** proposed; steering decided by the owner in this session.
- **Decision:** `ConversationTracker.TrySteer(conversation, input)` hands input to the run of the conversation going
  on in this process; the checkpoint layer injects it before the run's next model call and stores it, and the run
  does not end while steering input waits. It returns false when no run of the conversation is going on in this
  process, and the host starts a run with the input instead. Stopping a run is cancelling its token.
- **Alternatives:** a steering queue in the store, which works across processes but reads the store before every
  model call; queuing input for after the run, which is just the next run.

### D52. Plan and auto modes are built in

- **Status:** proposed; both modes decided by the owner in this session.
- **Decision:** `AgentMode` `Default`, `Plan`, `Auto`, a run option (`WithMode`, configurable like any run option).
  `Plan` offers only tools marked `ReadOnly()` (toolset tools, or MCP servers with `UseMcpServer(...).ReadOnly()`),
  `write_todos` and a built-in `exit_plan_mode(plan)`, which asks the host; `ApprovePlan(mode)` continues in `Default`
  or `Auto`, a rejection's reason sends the model back to planning. `Auto` runs tools that require approval unless a
  rule rejects the call. A conversation keeps its mode: a run without one continues in the mode the last run ended
  in, stored as an entry. Sub-agents run in their caller's mode.
- **Alternatives:** modes as tools the model calls (MAF's `mode_get`/`mode_set`), which lets the model leave plan
  mode unasked; a classifier built into auto mode, where a rule the host writes can ask any model it chooses.

## Repository

### D48. Carried as they were

- Sealing (v0 D19), CS1591 off in tests only (v0 D20), samples use a scripted client (v0 D21), package metadata
  (v0 D22), releases by tag through trusted publishing (v0 D37), the Typeshift name (v0 D38), the package README
  (v0 D39), nullable parameters widened at the schema root (v0 D12).

### D49. Diagnostics are numbered afresh

- **Decision:** nothing has shipped, so the catalog starts again at TSH001; after the first release IDs never change.
