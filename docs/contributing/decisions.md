---
title: Decisions
description: "Why the code is shaped the way it is: each non-obvious choice, the alternatives that lost, and where it lives."
---

# Decisions

Why the code is shaped the way it is. Each entry gives the decision, the alternatives that lost and where it lives.

Code cites an entry as `// D25: ...`, so IDs never change. Read the entry before you change the code it names. To change
the decision, edit the entry in the same commit, or add a new one and move the old to the [withdrawn](#withdrawn) table.

**Status** says where a decision came from:

| Status | Means |
|---|---|
| owner | The owner decided it during the rewrite. |
| approved | The owner approved it with the rewrite's API sign-off. |
| brief | The rewrite's brief decided it. |
| carried (v0 Dn) | It carries v0's decision `Dn` over, as it was or with the changes named. |

## Registration and the generator

### D1. `AddTypeshift()` exists only as an intercepted call

- **Status:** carried (v0 D1).
- **Decision:** the library's `AddTypeshift()` throws, saying what to check; the generator intercepts every call in a
  project. It returns `TypeshiftBuilder`, the one registration chain. A library that calls `AddTypeshift()` inside its
  own `AddX()` registers only its own modules; the docs say so.
- **Alternatives:** a generated method per assembly, ambiguous (CS0121) wherever `InternalsVisibleTo` reaches.
- **Where:** `DependencyInjection/`, `Emitting/InterceptorEmitter.cs`.

### D2. An analyzer reports the diagnostics; the generator only writes code

- **Status:** approved.
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

- **Status:** owner.
- **Context:** v0 re-bound every class with a base list on each edit (about 170 ms per keystroke for 50 toolsets).
- **Decision:** the syntax predicate takes classes whose base list names `Toolset`, `Agent` or
  `JsonSerializerContext` (any qualification); the semantic check confirms. No marker attribute.
- **Alternatives:** a marker attribute with `ForAttributeWithMetadataName`, which reruns only changed files but adds
  ceremony to every declaration.
- **Where:** `Parsing/Discovery.cs`.

### D4. Every declaration shape the generator cannot read is diagnosed

- **Status:** approved.
- **Context:** v0 ran the declaration chains at runtime too, and read the tool picks from syntax at compile time, so a
  chain split over variables offered one set of tools at runtime and generated code for another.
- **Decision:** `Include(t => t.X)` stays, picked from the generated `ToolList<T>` (v0 D29): a foreign tool does not
  compile. The generator reads the picks from syntax, since the list's properties are its own output; the analyzer
  reports every other shape: a chain split over variables, a pick that is not `t => t.X` (TSH015), a declaration
  outside the constructor body (TSH014). At runtime the chains declare nothing: the generated code is the one truth,
  and only values (`WithDisplayName`, chat options, `Configure{Tool}()`) are recorded.
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
- **Agents:** besides those, the hidden `Declaration` override, one private nested class `TypeshiftGenerated` (the
  declaration, the builders and the per-agent store of `Configure{Tool}()` values) and one private field
  `_typeshift`, so an agent gains as few names as possible. A tool's member in `Settings` named like an option or a
  record member gets a `Tool` suffix. A public agent's `Settings` cannot name the overrides of an internal toolset:
  TSH006, and the generator leaves the member out. Nor can it name a less accessible provider id type, which a
  configurable model or summary model brings in: TSH006 on the pick, and the generator writes the provider members
  internal. Its host members take ids of the build's id type, so a less accessible id type is TSH006 on the agent,
  and the generator writes the host members internal.
- **Where:** `Emitting/Names.cs`, `Emitting/AgentEmitter.cs`.

### D7. Stamps carry the generated names and a version

- **Status:** approved.
- **Context:** v0's stamps carried tool names only, so an agent rebuilt `{Tool}Overrides` names with its own
  generator version.
- **Decision:** tool stamps name the overrides type and the list property, and every module stamp has a stamp
  version; an unknown version is TSH020.
- **Where:** `Generated/Stamps.cs`, `Emitting/ModuleEmitter.cs`, `Parsing/ReferenceReader.cs`.

### D8. The generator targets Roslyn 5

- **Status:** approved.
- **Decision:** consumers need C# 14 for the extension blocks anyway, so the generator uses Roslyn 5 APIs, and v0's
  4.14 pin and its trap are gone.

### D9. Every project that declares toolsets or agents references the package

- **Status:** carried (v0 D9). TSH020 catches a reference without it.

### D10. The hidden public surface generated code calls

- **Status:** carried (v0 D35).
- **Decision:** what only generated code calls is public and `[EditorBrowsable(Never)]`, in `Typeshift.Generated`.

### D11. The id type is an MSBuild property

- **Status:** owner.
- **Context:** ids are typed end to end and the library is compiled before the host chooses its id type.
- **Decision:** `<TypeshiftIdType>int</TypeshiftIdType>`, set once in `Directory.Build.props`, `Guid` by default.
  The library's id-carrying types are generic (`ITypeshiftStore<TId>`); the generator writes per project
  `global using` aliases (`ITypeshiftStore`, `InMemoryTypeshiftStore`, `ConversationTracker`, `LiveRun`) and
  generates typed members with the concrete type. A reference built with another id type is TSH021; a store of
  another is TSH028. The interceptor closes the core services over the id type (`AddCoreServices<TId>`), so
  `WithStore<T>()` needs none; behind the typed members, the library passes ids boxed to the store of that type.
  Module stamps carry the id type.
- **Alternatives:** `AddTypeshift().WithIdType<int>()`, known only in the registering project, with generic run
  members checked at runtime; a generated interface per project, ambiguous under `InternalsVisibleTo`.
- **Where:** `buildTransitive/Typeshift.props`, `Emitting/AliasEmitter.cs`.

### D12. Typed host members are generated extension blocks

- **Status:** carried (v0 D4).
- **Decision:** `extension(AgentHost<TAgent> host)` per agent, in the agent's assembly, with `RunAsync`, `RunAsync<T>`,
  `RunStreamingAsync`, `ResumeAsync`, `ResumeStreamingAsync`, `LoadSettingsAsync`, `SaveSettingsAsync`, `ConfigureAsync`
  and `ResolveOptionsAsync`. They call hidden library methods, so the logic lives and is tested in the library.

## Declarations

### D13. Tool names derive from the method

- **Status:** owner.
- **Decision:** `SearchTickets` is `search_tickets`; an `Async` suffix is dropped, so `CloseTicketAsync` is
  `close_ticket`. `WithName` overrides. The name keys stored settings, so renaming a method renames the tool: the docs
  say to pin the old name with `WithName` first. TSH007 also compares the generated property names.
- **Alternatives:** keeping `Async` (`close_ticket_async`), which leaks a C# convention to the model; a stored key
  separate from the name, a second identity.
- **Where:** `Parsing/ToolsetParser.cs`.

### D14. Descriptions come from XML docs

- **Status:** carried (v0), `WithDescription` overrides; parameters the model never sees need no `<param>` (D2's
  suppressor).

### D55. The types tools take describe themselves with their XML docs

- **Status:** owner.
- **Context:** a `<param>` doc described a parameter, but nothing described the properties of the record it took,
  so a command's ids and codes reached the model without the docs that say how to fill them.
- **Decision:** the package depends on Weboost.SchemaAnnotations and passes its generator to every consumer. The
  package's build adds a hidden `[SchemaAnnotations]` class to each project with `GenerateDocumentationFile`, named
  after the assembly, and the module registers the docs collected for every marked class of its assembly. Tool
  schemas are built through an annotator over all of them. `TypeshiftSchemaAnnotations=false` leaves the class out.
- **As built:** the annotator never replaces a description, so a `<param>` doc keeps describing its parameter. A
  project that marked its own class has two, and Weboost.SchemaAnnotations then writes no
  `AIJsonSchemaCreateOptions.Annotated`; deleting its own brings `Annotated` back from the added class. A project
  that declares no toolsets or agents but documents types gets a module that registers just those docs, which the
  `AddTypeshift()` of a project referencing it registers like any referenced module.
- **Where:** `buildTransitive/Typeshift.props`, `Parsing/Discovery.cs`, `Emitting/ModuleEmitter.cs`,
  `Generated/ToolJson.cs`.
- **Alternatives:** a chain call taking `AIJsonSchemaCreateOptions`, which every host would have to make; the
  generator reading the docs itself, a second copy of Weboost.SchemaAnnotations; finding a class the project marked,
  one more line of setup per project.

### D15. Tools receive services through `IServiceProvider`

- **Status:** owner.
- **Context:** the brief asked to investigate injecting resolved services as parameters.
- **Decision:** tools stay static and take `IServiceProvider`, the run's services. No injection.
- **Alternatives:** `[FromServices]` parameters (one attribute, TSH for an interface parameter without it); inference
  by type (interfaces are services), leaky for collection interfaces and concrete services such as a `DbContext`.

### D16. A tool receives its settings as a parameter

- **Status:** carried (v0 D40). The settings, `IServiceProvider`, `CancellationToken` and `AIFunctionArguments` are
  bound by type and left out of the schema; `ref` and `out` are TSH004.

### D17. One flat chain per declaration

- **Status:** carried (v0 D41), brief. No builder inside a lambda anywhere: toolsets, agents,
  registration. Metadata goes through `With...` calls, capabilities through `Use...` calls.

### D18. Configurable members are an allow list unless `WithConfigurableSettings` says otherwise

- **Status:** carried (v0 D43).

### D19. Configurable types widen to what the generator can serialize

- **Status:** owner.
- **Decision:** besides v0's scalars, enums and string lists: `DateOnly`, `TimeOnly`, `Uri`, records, record structs
  and classes of storable settable members (replaced as a whole, never merged), arrays, lists and sets of storable
  values, dictionaries keyed by `string` or an enum. The generator writes their serialization member by member, as
  for the records themselves; anything else is TSH012. Recursive types are TSH012.
- **Details:** a stored record or class needs a public parameterless constructor; its members without a setter are
  computed and not stored, its obsolete ones no longer stored. A missing member reads as the constructor's value.
  Collection items and dictionary values are values: a nullable value type item has no stored form. A record struct
  stores like a record. Other structs than the scalars are not storable, since most keep their state where no setter
  reaches and would store as an empty object.
- **Where:** `Parsing/Storage.cs`, `Emitting/StorageEmitter.cs`, `Generated/StorableJson.cs`.

### D20. `Configure{Tool}()` stays

- **Status:** carried (v0 D30), brief.
- **Decision:** a generated private method per offered tool with settings, returning a builder with a `With...` per
  settable member. Nothing shorter fits the one-chain rule, since a picker (`Configure(t => t.X)`) still needs a
  generated builder per tool and adds a step. A `Configure{Tool}()` the agent declares itself replaces the
  generated one.

### D21. Instructions come from a virtual method

- **Status:** carried (v0 D42), brief.

### D22. Agent names are optional and derive from the class

- **Status:** carried (v0 D31), changed.
- **Decision:** without `WithName`, an agent goes by its class name in snake_case, like a tool, so it can be a tool or
  a handoff target. Names need not be unique; nothing is stored by name (D39).

### D23. No agent inheritance, no composition block in v1

- **Status:** owner.
- **Decision:** agents and toolsets derive from `Agent` and `Toolset` directly (TSH017). Shared declarations come
  from static methods and constants. A preset class folded into agents (`UsePreset<T>()`) was considered and
  deferred.

### D57. An agent may declare tools of its own

- **Status:** owner.
- **Context:** a tool only one agent needed still took a toolset class of its own and a `UseToolset<T>()`.
- **Decision:** `AddTool(method)` in an agent's constructor, with the toolsets' chain, backed by a static method of the
  agent. The agent offers its own tools after its toolsets' tools. They do not form a toolset: they get no tool list, no
  stamp and no registration, so no other agent offers them and `Include` and `Exclude` do not pick them.
- **As built:** the generator reads them with the toolsets' parser and writes their overrides, factories and functions
  into the agent, where the agent's `Settings`, its `Configure{Tool}()` methods and its declaration list them as they
  list a toolset's tools. The analyzer applies every tool rule; a name is unique across the toolsets and the agents' own
  tools of the compilation and the toolsets of its references (TSH007), and against the agent's toolsets (TSH025). The
  module stamp lists the own tools that require approval, for TSH024. No stamp carries an agent's own tools, so TSH007
  does not compare them with those of a referenced project's agents.
- **Where:** `Agents/Agent.cs`, `Parsing/ToolsetParser.cs`, `Parsing/AgentParser.cs`, `Parsing/Catalog.cs`,
  `Emitting/AgentEmitter.cs`.
- **Alternatives:** a second, lighter base class for small tasks, such as `AgentTask<TResult>` with instructions and
  a typed result, a second concept beside `Agent` (the owner's open question); `AIFunctionFactory` functions in
  `WithTools`, which reflect, skip the analyzer and settings, and need JSON metadata the generator never checks;
  own tools pickable from a `ToolList<TAgent>`, which would let one agent offer another's tools.

### D24. Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules

- **Status:** approved.
- **Decision:** `RequiresApproval()` wraps the tool in `ApprovalRequiredAIFunction`; `FunctionInvokingChatClient` ends
  the run with `ToolApprovalRequestContent`s, listed by `response.ApprovalRequests`. The host answers with
  `CreateResponse(approved)` in the next run of the conversation; the stored request is the one that runs, an answer to
  no stored request is dropped, a request keeps its first answer, and an unanswered request is left out of the next run.
  A request in the input is dropped before anything stores it, so a host's input cannot bring a call of its own past the
  rules. Standing approvals hold beyond what compaction left: a summary entry stores them. Calls in the same
  response that need no approval wait with it and run once the host answered (the tool loop asks for every call of a
  response once one needs approval; the approval layer approves the others). A run that asks without a conversation
  throws, naming the tool. `request.ApproveForConversation()` approves the tool for the rest of the conversation; the
  approval is stored with it, so it holds across runs and processes. `IToolApprovalRule`s (`WithApprovalRule<T>()`,
  scoped) decide calls before the host is asked, seeing the exact arguments: `Approve`, `Reject(reason)` (wins over
  every approval) or `Abstain`, which the mode resolves (D52). A layer above `FunctionInvokingChatClient` answers the
  requests rules and standing approvals decide and continues the loop, so only the rest reach the host. It also stores
  the requests and its own answers when a run ends asking, as `FunctionInvokingChatClient` keeps them out of what the
  checkpoint layer below it sees. Approvals match tools by name, which is unique per agent (TSH007, and a runtime check
  for MCP tools).
- **Where:** `Approval/ApprovalChatClient.cs`, `Approval/ToolApproval.cs`.
- **Alternatives:** an approval callback the run awaits, which holds a run open across requests; a Typeshift approval
  type, a second vocabulary.

## Runtime

### D25. A run uses the services of the scope its host was resolved from

- **Status:** owner.
- **Context:** v0 gave each tool call a fresh scope of the root container, so request-scoped services (the current
  customer) never reached tools, context or the chat client provider (audit finding 2).
- **Decision:** `AgentHost<TAgent>` is transient and keeps the provider it was resolved from. Tools, context
  providers, middleware, the MCP tool provider and the chat client provider resolve from it. Agents and toolsets stay
  singletons, the store too.
- **Alternatives:** a run-context tool parameter carrying the typed id, which built-in toolsets compiled before the
  host cannot type; per-call scopes, which lose the request.
- **Where:** `AgentHost.cs`.

### D26. Runs return Microsoft.Extensions.AI's types

- **Status:** owner.
- **Decision:** `RunAsync` returns `ChatResponse`, `RunAsync<T>` `ChatResponse<T>`, `RunStreamingAsync`
  `ChatResponseUpdate`s; messages carry the answering agent as `AuthorName`. Inputs are a `string` or a
  `ChatMessage` (for images and approvals). The raw path (`ResolveOptionsAsync`, `CreateChatClientAsync`, the client)
  stays; options it resolved remember their settings id, so the conversation id is the only string.
- **Alternatives:** an own `AgentResponse`, a type to learn for what `ChatResponse` holds.

### D27. Four settings layers

- **Status:** brief. Code defaults, the agent's code, its stored `Settings`, the run. Stored tool
  defaults (`ToolDefaults`) are gone, so resolving a run's settings is one document.

### D28. The model is a configurable option like any other

- **Status:** owner; changes v0 D34.
- **Decision:** `Configurable(o => o.ModelId)` puts `Model`, and its `Provider` (D56), into the agent's `Settings`,
  overriding `WithModel`; without it the model stays in code. The allow list holds for the model too. The options
  that may be configurable are the model, the instructions, the sampling options, the stop sequences, multiple tool
  calls and reasoning; the rest belong to the run or have no stored form (TSH012).
- **Alternatives:** `Model` in every `Settings` (v0, the brief's first sketch), which lets a settings form a
  customer edits pick any model the provider knows.

### D56. A model is a provider and a model, one interface serves both

- **Status:** owner.
- **Context:** a model was one string the host's provider read, so a host with several providers invented composite
  names such as `lmstudio/qwen/qwen3-8b`, split them again, and wrapped each client to put the provider's own id back
  into the request.
- **Decision:** `WithModel(provider, model)`. `IChatClientProvider<TProviderId>` has one method,
  `GetChatClientAsync(provider, model, ct)`, resolved scoped from the run's services, and `ChatOptions.ModelId`
  carries the model alone. The provider id type is the MSBuild property `TypeshiftProviderIdType`, `string` by
  default, a type settings store as one value (a string, a number, a `Guid` or an enum), with a generated alias as D11
  has for ids. The summary model is the run's model unless named, and its provider the run's provider: the agent's
  code names a summary model only, and only settings (`SummaryProvider`) name another provider for it.
- **As built:** the library keeps providers boxed (`Agent.Provider`, `ResolvedChatOptions.Provider` and
  `SummaryProvider`), and `ChatClients<TProviderId>`, which the interceptor's `AddCoreServices<TId, TProviderId>`
  registers, unboxes them for the host's provider. Generated `ApplySettings` sets the providers through
  `SetProvider` and `SetSummaryProvider`. A provider of another type at `WithModel` or `WithChatClientProvider<T>()`
  is TSH030; a provider id type that cannot be used is TSH029. Module stamps carry the provider id type next to the
  id type, and a reference with agents built with another is TSH021.
- **Where:** `Hosting/IChatClientProvider.cs`, `Hosting/ChatClients.cs`, `Agents/ChatOptionsBuilder.cs`,
  `Compaction/Compaction.cs`, `Parsing/IdTypes.cs`, `Emitting/OptionMembers.cs`, `Generated/Stamps.cs`,
  `Emitting/ModuleEmitter.cs`, `Parsing/ReferenceReader.cs`.
- **Alternatives:** a model record with a request type, a client cache and a catalog, more surface than the problem
  needs; methods that list providers and their models, which Typeshift never calls; a summary provider in code, tied
  to a provider the user may switch away from; compaction that drops turns without a summary model.

### D29. The chat client pipeline

- **Status:** approved.
- **Decision:** outermost first: conversation tracking (resume, run boundaries, live updates, the `invoke_agent`
  span), run middleware, context providers, approval (standing approvals and rules), `FunctionInvokingChatClient`
  (handoff, run options, tool middleware as its `FunctionInvoker`), checkpoints (each step appended before compaction
  sees it; steering input and background answers injected here), compaction, `OpenTelemetryChatClient`, the
  provider's client behind a wrapper that never disposes it.
  Checkpoints and compaction sit inside the tool loop, so they act before every model call. The approval layer builds
  a `FunctionInvokingChatClient` per round, whose iteration limit is that round's, so calls on one client never share
  a limit.
- **Where:** `AgentHost.cs`.

### D30. Middleware: one interface around runs, one around tool calls

- **Status:** owner.
- **Decision:** `IRunMiddleware.InvokeAsync(run, next, ct)` serves streaming and non-streaming runs alike: it may
  change messages and options, read `run.Response` after `next`, or set it instead of calling `next`.
  `IToolMiddleware` wraps `FunctionInvocationContext`. Registered in the chain, in order, for every agent; a
  middleware filters by `run.Agent`. Changes to the messages hold for the pass; the conversation keeps what the
  host sent. A response set without `next` is stored with the conversation, and a pass that called no model ends
  the run, storing the steering input that waits. In a streaming
  run the middleware awaits `next` while the updates go on to the caller through a channel, which reads the model's
  stream ahead of a slow caller. A steered run passes through again for steering input that arrives after its
  answer.
- **Where:** `Middleware/`, `FunctionInvokingChatClient.FunctionInvoker` for tool middleware.
- **Alternatives:** MAF's separate run and streaming delegates (streaming falls back to non-streaming when one is
  missing); per-agent middleware in the agent's chain, a second place.

### D31. Context providers

- **Status:** approved.
- **Decision:** `UseContext<T>()`; `IContextProvider.GetContextAsync(run, ct)` returns an `AgentContext` of
  instructions, messages and tools, appended to the run's. Called after resume, before the model, on each pass of
  a run (a steered run makes more than one); its messages are never stored. Providers are resolved from the run's
  services, or created from them when none is registered. No after-run hook: run middleware covers it.
- **Where:** `Context/`.

### D32. Run options, with compaction inside the tool loop

- **Status:** owner.
- **Context:** the first draft compacted only when a conversation resumed, so one long run outgrew its context, and
  the tool loop's limits were Microsoft.Extensions.AI's constants (40 iterations).
- **Decision:** `WithRunOptions()`: `WithMaxToolIterations`, `WithMaxConsecutiveToolErrors`,
  `WithConcurrentToolCalls`, `WithMaxInputTokens`, `WithTargetInputTokens`, `WithSummaryModel`, and `Configurable`
  over the `RunOptions` record, so a run option joins `Settings` like a chat option. Before each model call, when the
  last call's reported input tokens reached the maximum, the history shrinks to the target: oldest tool results
  cleared first (calls kept), then the oldest turns summarized, by the run's model unless one is named (D56).
  Instructions, the latest `write_todos` call and call/result pairs stay; the summary is stored as an entry later
  runs start from. Without reported usage, tokens are estimated from the text.
- **As built:** the estimate counts characters (four to a token) over the messages, the instructions and the tool
  schemas, calibrated by the tokens the provider reported for the run's last call, and before the run's first call
  for the conversation's last. Only results the model already
  answered after are cleared. What compaction did holds for the rest of the run: the tool loop keeps the whole
  history, so each later call gets the same cleared results and summary. The summary entry holds the summary, the
  kept todo list and copies of the kept messages, with the run's mode, its standing approvals and the background calls
  still working, so a later run loads only that entry and the ones after it (`LoadConversationSinceCompactionAsync`,
  which a store may override to leave the rest unread). A summary an earlier version stored keeps no standing approvals,
  and its runs load the whole conversation. Rounds the approval layer continues share `MaxToolIterations`, and so do the
  passes steering input or background answers start. Calls the host approves run in the conversation's next run, which
  counts its own rounds.
- **Where:** `Compaction/`, `Approval/ApprovalChatClient.cs` for the shared limit.
- **Alternatives:** MAF's strategy pipeline and trigger combinators, more concepts for the same two steps; a
  tokenizer dependency; message-count triggers, which ignore that one tool result may outweigh a hundred messages; a
  separate `WithCompaction()` chain next to the limits.

### D33. Sub-agents, in the foreground or the background

- **Status:** owner.
- **Decision:** `UseAgent<T>()` offers a tool named like the agent, with its description and one `input` string. A
  call runs it in a fresh context with the same settings id, the caller's mode, standing approvals and rules, and no
  conversation of its own, and returns its text. `.InBackground()` makes the tool return at once; the answer reaches
  the calling run as a message before a later model call, and the run does not end while sub-agents work. A
  sub-agent cannot ask the host: in the default mode its calls that need approval and that nothing approves are
  rejected (TSH024 warns). Circles are TSH026.
- **As built:** the sub-agent's tool is a runtime `AIFunction` that resolves the agent's `AgentHost<T>` through a
  factory `UseAgent<T>()` captured, so no generated code is needed; it finds its caller's run through
  `FunctionInvokingChatClient.CurrentContext`. Background answers join the run through the steering queue, so
  every run has a `ConversationRun`, which stores only with a conversation; the run's end cancels the sub-agents
  still working, and the next run tells the model which ones it lost, after a crash, a cancellation or a failure,
  including the ones a summary left working. TSH023 and TSH024 check the agents a compilation uses, a reference's
  through its module stamp, which carries the agent's name, whether it has a description and the tools it offers that
  require approval; tool stamps carry `RequiresApproval` for that list. TSH026 checks the compilation's own agents,
  since assemblies cannot reference each other in a circle.
- **Where:** `Agents/SubAgentFunction.cs`, `Conversations/ConversationRun.cs`, `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** passing a sub-agent's approval requests up to the caller's host, which pauses a parent run on a
  child's call and needs the child's state stored to resume (possible later); MAF's six background-task tools
  (start, wait, results, continue, clear), where one tool and an injected answer do the job; storing each sub-agent
  run as a conversation of its own, which needs ids the library may not create.

### D34. Handoff continues with the agent handed to

- **Status:** owner.
- **Decision:** `UseHandoff<T>()` adds a declaration-only tool `handoff_to_{name}`. Calling it ends the current
  agent's loop; the target answers in the same run with the whole conversation. The conversation's last entry names
  the agent that answers its next run, if it is reachable through the host agent's handoffs. Handoff needs a
  conversation; run settings apply to the host's agent only.
- **As built:** the conversation layer sees the declaration's call the tool loop passes out, stores its result as an
  entry of the agent handed to, and lets that agent resume the run; a run without a conversation does not offer
  handoffs, and a call of an agent it does not hand off to is answered by the tool loop as an unknown tool. Tool results
  after which the tool loop made no model call are stored before the run ends, a handoff's and a terminating tool's
  alike.
- **Where:** `Conversations/ConversationChatClient.cs`.

### D35. MCP servers through a host provider

- **Status:** approved.
- **Decision:** `UseMcpServer(name)` with `.RequiresApproval()`; the host's `IMcpToolProvider` returns the server's
  tools, connected with the MCP client of its choice. Typeshift takes no MCP dependency. Provider-side servers use
  `HostedMcpServerTool` in `WithTools`.
- **As built:** the servers' tools are asked for when a run's options are resolved, after the agent's own;
  `ReadOnly()` marks them for plan mode, `RequiresApproval()` wraps them in `ApprovalRequiredAIFunction`. A tool
  named like the agent's own fails the run.
- **Where:** `Mcp/Mcp.cs`, `Hosting/AgentHost.cs`.

### D36. OpenTelemetry is always on

- **Status:** approved.
- **Decision:** source and meter `Typeshift`, GenAI conventions (`invoke_agent`, `chat`, `execute_tool`, operation
  duration, token usage); message content only with `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT`. No API.
- **As built:** the conversation layer opens the `invoke_agent` span, so it covers middleware and context too.
  Microsoft.Extensions.AI's `OpenTelemetryChatClient` writes the chat spans and the metrics: one per provider and model,
  created the first time a run uses them and kept as long as the host (`InstrumentedClients`), which passes each call on
  to the client its run got. One per run, disposed with it, reset the metrics after every run, since the OpenTelemetry
  SDK ends the instruments of a disposed meter; one per client instance did the same for a provider that hands out a
  client per run or per tenant. The tool loop writes `execute_tool` spans with its source. A store that fails records
  the exception on the run's span.
- **Where:** `Telemetry/Telemetry.cs`, `Telemetry/InstrumentedClients.cs`.

### D37. The built-in toolsets

- **Status:** owner.
- **Decision:** in `Typeshift.Toolsets`, declared with Typeshift's own toolset API:
  - `TodoToolset`: `write_todos` replaces the whole list; the list lives in the conversation (the latest call).
  - `FileSystemToolset`: six tools within an `IFileSystemRoot` the host registers per scope; absolute paths, `..`
    and escaping links refused; the three reading tools `ReadOnly()`, `delete_file` behind approval.
  - `ShellToolset`: `run_command` in the root, always behind approval; timeout and output cap as settings.
  - `WebToolset`: `web_search` and `web_fetch` over `IWebSearch` and `IWebFetch`, no provider shipped.
- **As built:** the library compiles them with its own generator; its module stamps no id type, as it has no
  agents, and every consumer's `AddTypeshift()` registers it. The file tools follow every link on a path, the root's
  own included, before comparing it with the root, and return why they refuse instead of failing the call, so the
  model can correct itself.
- **Where:** `Toolsets/`.
- **Alternatives:** MAF's five todo tools with state in the session (more tokens, a store per session); file access
  in a store abstraction.

### D53. A failed tool call reports its exception's message

- **Status:** owner.
- **Context:** Microsoft.Extensions.AI's tool loop hides why a call failed unless `IncludeDetailedErrors` is on, so
  the model read only "Error: Function failed." and could not correct a missing argument or an unknown id.
- **Decision:** the tool loop always passes the exception's message to the model. Tool authors write messages that
  say what to do instead, and keep secrets out of them, since the provider receives them.
- **Where:** `Hosting/AgentHost.cs`.
- **Alternatives:** a run option, off by default, which every agent would have to turn on to recover from its errors;
  catching exceptions in tool middleware, which hides the failure from `MaxConsecutiveToolErrors`.

## Storage

### D38. One store of documents and conversations

- **Status:** approved.
- **Decision:** `ITypeshiftStore<TId>` (aliased `ITypeshiftStore`): `LoadAsync` and `SaveAsync` of documents as
  JSON, keyed by id and kind, with a version and a revision; `LoadConversationAsync` and `AppendConversationAsync` of
  `ConversationEntry`s keyed by the conversation id. `LoadConversationSinceCompactionAsync`, which runs call, defaults
  to the whole load and drops what came before the last summary. The library serializes the documents (each agent's
  `Settings`), so a store implements no generic method and handles strings and numbers only.
- **Alternatives:** v0's load and save only, which rewrote a whole conversation per turn; a generic append for any
  kind, a concept with one user; `LoadAsync<TData>` and `SaveAsync<TData>` over `IStorable` documents (up to
  0.1.0-alpha.3), which made every store call `TData.Kind`, `Serialize` and `Deserialize` itself; one document per
  settings id holding every agent's settings, which would hide the versions a store migrates by (D40).

### D39. Kinds are stable names

- **Status:** owner.
- **Decision:** `IStorable.Kind`: for an agent's `Settings` its `WithStorageKind("...")` if it has one, or else its
  class name; `conversation_entry` for conversation entries. Moving an agent to another namespace keeps its data;
  renaming the class keeps it once `WithStorageKind` pins the old name. Two agents of one kind are TSH022.
- **Alternatives:** the agent's name, which would need unique names (out of scope); the full type name (v0).

### D40. A schema version per document

- **Status:** owner.
- **Decision:** `IStorable.Version`, 1 for `Settings` until `WithSettingsVersion(n)` raises it. The store keeps it
  next to the JSON and returns it with a load, and `Deserialize` refuses a newer version; migrating older documents is
  the store's job.
- **Alternatives:** a version hashed from the record's shape (automatic, but no store can map it to a migration); a
  migration hook on the agent; an analyzer against a shipped list of stored members (later).

### D41. Optimistic concurrency

- **Status:** owner.
- **Decision:** `SaveAsync` takes the revision the caller loaded, 0 for none, and throws `StoreConflictException`
  when the stored one differs. `ConfigureAsync` reloads and reapplies until it wins (each conflict means another
  save won), at most ten times, so a store that conflicts every time fails instead of spinning. `SaveSettingsAsync`
  takes the revision from `LoadSettingsAsync`.

### D42. Settings ids and conversation ids are separate; conversations grow by appending

- **Status:** owner.
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
  endpoint) writes the stored shape too. `ConversationEntry` carries the same converter, and a `TypeConverter` to
  `string` that Newtonsoft.Json writes the stored JSON through, so a store hands entries to either library as they are.
- **Details:** each overrides record writes and reads itself (`IJsonStorable<T>`), so an agent's `Settings` in
  another assembly than the toolset delegates to it. A member that does not read, missing or mistyped, reads as
  null and keeps the layer below; a document that is no JSON object throws. Enums are stored by name, non-finite
  numbers as strings.

### D45. Tool JSON: contexts from the modules, reflection outside AOT

- **Status:** approved.
- **Decision:** modules register the `JsonSerializerContext`s that list their tools' types (v0 D44). Outside Native
  AOT, Microsoft.Extensions.AI's reflection-based defaults cover the rest, so tools need no context; under AOT
  (`PublishAot` or `IsAotCompatible`), a type no context lists is TSH027.
- **As built:** TSH027 knows the types Microsoft.Extensions.AI's own context lists (strings, numbers, `bool`, dates,
  `TimeSpan`, JSON elements) and the contexts of the compilation; a tool returning `IAsyncEnumerable<T>` returns a
  `List<T>`, which it checks. The console sample publishes as Native AOT in CI, where a trim or AOT warning fails the
  build.
- **Where:** `Analysis/TypeshiftAnalyzer.cs`, `samples/Typeshift.Samples.Console`.

## Conversations

### D46. Tracking, carried and fixed

- **Status:** carried (v0 D18), with the audit's fixes.
- **Decision:** tracking wraps the tool loop and marks where a run begins and ends; checkpoints inside it store
  each step (D50). The provider never receives the
  conversation id. On resume, unanswered calls and error messages are left out. A failed save is logged and traced,
  and the answer returns. The agent's name goes on `AuthorName`, never into `AdditionalProperties`.
- **Details:** the end of a run is an entry of its own (`EndsRun`), with an `ErrorContent` when the run failed, was
  cancelled or its stream was left early. The checkpoints know what they stored by reference, since the tool loop
  hands the same messages on; a streamed response is stored from the updates, and the tool loop's own messages
  built from those updates are matched by count.
- **Where:** `Conversations/ConversationChatClient.cs`, `Conversations/CheckpointChatClient.cs`,
  `Conversations/ConversationHistory.cs`.

### D47. Live updates are a feed per conversation

- **Status:** approved.
- **Decision:** `WithRealtimeTracking(options)` turns publishing on; `ConversationTracker.WatchAsync(conversation)`
  reads a bounded feed that drops its oldest updates when the watcher falls behind. A run never waits
  for a watcher. `PublishInterval` (100 ms) and `MaxPendingUpdates` (256) are options. `Runs` lists the runs of
  conversations going on in this process.
- **Alternatives:** v0's process-wide events, awaited up to 5 s per model call.
- **Details:** merged text waits for the next update or the end of the stream, so no timer runs per watcher. A run
  that does not stream publishes each response and tool result as it stores them, through the same publisher.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/LivePublisher.cs`.

### D50. A conversation is stored step by step and resumes after a crash

- **Status:** approved.
- **Context:** the first draft stored a run when it ended, so a run that died after an hour of work lost all of it.
- **Decision:** a conversation is a log of `ConversationEntry`s: a run's input, each model response (with model and
  usage), each tool result, compaction summaries; the run's last entry has `EndsRun`. Checkpoints append each step
  the moment it exists, tool results before the next model call. A run without an ending entry was interrupted:
  `ResumeAsync(conversationId)` (and `ResumeStreamingAsync`) continues it without new input, leaving out a call that
  was in flight; a new run on the conversation also starts from its steps. One run per conversation at a time in a
  process; across processes it is the host's to ensure. Resuming a conversation whose last run ended throws.
- **Alternatives:** one entry per run, rewritten at each step (a whole-run write per model call); re-running the
  interrupted tool call, unsafe for side effects; a `RunAsync` with a null input in place of `ResumeAsync`.
- **Where:** `Conversations/`, the checkpoint layer of D29.

### D51. Steering a run going on

- **Status:** owner.
- **Decision:** `ConversationTracker.TrySteer(conversation, input)` hands input to the run of the conversation going
  on in this process; the checkpoint layer injects it before the run's next model call and stores it, and the run
  does not end while steering input waits. It returns false when no run of the conversation is going on in this
  process, or once the run has decided to end, and the host starts a run with the input instead. Stopping a run is
  cancelling its token. Steering input goes in after the message the tool loop's messages ended with when it
  arrived, found by reference in every later call of the run, so context that grows between passes cannot move it
  between a call and its result. Input a run took and no model received is stored with the run's end or its
  handoff.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/ConversationRun.cs`.
- **Alternatives:** a steering queue in the store, which works across processes but reads the store before every
  model call; queuing input for after the run, which is just the next run.

### D52. Plan and auto modes are built in

- **Status:** owner.
- **Decision:** `AgentMode` `Default`, `Plan`, `Auto`, a run option (`WithMode`, configurable like any run option).
  `Plan` offers only tools marked `ReadOnly()` (toolset tools, or MCP servers with `UseMcpServer(...).ReadOnly()`),
  `write_todos` and a built-in `exit_plan_mode(plan)`, which asks the host; `ApprovePlan(mode)` continues in `Default`
  or `Auto`, a rejection's reason sends the model back to planning. `Auto` runs tools that require approval unless a
  rule rejects the call. A conversation keeps its mode: a run without one continues in the mode the last run ended in,
  stored on the run's ending entry and on a summary; a run whose settings name a mode switches it. Plan mode appends a
  short instruction to research, then call `exit_plan_mode`. Sub-agents run in their caller's mode; plan mode offers
  them, and in it a sub-agent gets its read-only tools and no `exit_plan_mode`, and answers its caller.
- **Where:** `Agents/RunOptions.cs`, `Approval/ExitPlanModeFunction.cs`, the approval layer of D29.
- **Alternatives:** modes as tools the model calls (MAF's `mode_get`/`mode_set`), which lets the model leave plan
  mode unasked; a classifier built into auto mode, where a rule the host writes can ask any model it chooses.

### D54. Runs starting and ending are a feed too

- **Status:** owner.
- **Context:** a list of running conversations, or a dot on each, had to poll `Runs` every second.
- **Decision:** `ConversationTracker.WatchRunsAsync(ct)` yields the runs going on, then each run as it starts and as
  it ends (`LiveRun.Ended`), from a bounded feed per watcher like D47's, sized by `MaxPendingUpdates`. It works
  without `WithRealtimeTracking()`, as `Runs` does.
- **As built:** the watch subscribes before it lists the runs going on, so a run starting at that moment is listed or
  fed, and its start is not reported twice.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/ConversationTracker.cs`.
- **Alternatives:** C# events, where a slow or throwing handler runs inside the run; a separate change record, which
  needs one more id-type alias.

## Repository

### D48. Carried as they were

- Sealing (v0 D19), CS1591 off in tests only (v0 D20), samples use a scripted client (v0 D21), package metadata
  (v0 D22), releases by tag through trusted publishing (v0 D37), the Typeshift name (v0 D38), the package README
  (v0 D39), nullable parameters widened at the schema root (v0 D12).

### D49. Diagnostics are numbered afresh

- **Decision:** the rewrite started the catalog again at TSH001, since none of v0's IDs had shipped. Once a release
  ships a diagnostic's row, its ID never changes (see [Releasing](./releasing.md#cutting-a-release)).

## Withdrawn

| ID | Was | Replaced by |
|---|---|---|

## Related

- [Architecture](./architecture.md): the parts these decisions shape.
- [Status](./status.md): what is built and what is open.
