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
  takes a report callback, a no-op in the generator. Compilation-wide checks (unique names, references, the provider id
  type) are compilation-end actions. The analyzer sees generated code, so it checks picks and the store type
  semantically. A `DiagnosticSuppressor` silences CS1573 for tool parameters the model never sees.
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
- **Decision:** per toolset a `{Tool}Overrides` record per tool with configurable settings and a
  `TypeshiftTools_{full name}` extension block in the global namespace (D12); per agent the nested `Settings`
  record, a private `Configure{Tool}()` per offered tool with settings and a `TypeshiftHost_{full name}` extension
  block in the global namespace (D12); per assembly a module. Keywords get `@`. A clash with a user member is
  TSH018, and so is
  a class named like a member written into it. Two tools of one class that would share a generated member are
  TSH007. A generated file is named after its class's full name. The compiler compares those file names without
  case, so where two full names differ only by case, each file's name also carries a hash of its class's spelling.
- **Names a generated type already has:** a generated property never takes the name of a member its type has from
  `object` (`Names.ObjectMembers`) or declares itself, and gets a suffix instead: `Tool` on the tool list and in an
  agent's `Settings`, `Value` in an overrides record. Each type keeps a list of its own members beside the shared
  one. The lists are not merged, because a suffix is part of the generated API: a tool `clone` is `CloneTool` in
  `Settings`, which has a `Clone`, and plain `Clone` on the tool list, which has none. Inside a generated record,
  its members are read through `this.`, so a settings member named like a parameter or a local of the generated
  method, such as `settings` or `writer`, binds to the member.
- **Module names:** `{Assembly}Module`, with every character of the assembly's name that is no letter or digit as
  `_`. A name of letters, digits and dots maps to one identifier alone and stays as it is. Any other name carries a
  hash of itself, `Acme_Tools_1a2b3c4dModule` for `Acme-Tools`, which would otherwise share `Acme_ToolsModule` with
  `Acme.Tools`. The hash is FNV-1a, the same on every machine. The name travels in the `[GeneratedModule]` stamp
  (D7), so no reader derives it.
- **Agents:** besides those, the hidden `Declaration` override, one private nested class `TypeshiftGenerated` (the
  declaration, the builders and the per-agent store of `Configure{Tool}()` values) and one private field
  `_typeshift`, so an agent gains as few names as possible. A tool's member in `Settings` named like an option or a
  record member gets a `Tool` suffix, and one more while another offered tool has that name: beside `kind_tool`,
  the tool `kind` is `KindToolTool`. A public agent's `Settings` cannot name the overrides of an internal toolset:
  TSH006, and the generator leaves the member out. Nor can it name a less accessible provider id type, the agent's
  own (D77), which a configurable model or summary model brings in: TSH006 on the pick, and the generator writes the
  provider members internal.
- **Where:** `Emitting/Names.cs`, `Emitting/AgentEmitter.cs`, `Emitting/ToolsetEmitter.cs`,
  `Emitting/ModuleEmitter.cs`, `Emitting/StorageEmitter.cs`.

### D7. Stamps carry the generated names and a version

- **Status:** approved.
- **Context:** v0's stamps carried tool names only, so an agent rebuilt `{Tool}Overrides` names with its own
  generator version.
- **Decision:** tool stamps name the overrides type and the list property, and every module stamp has a stamp
  version; an unknown version is TSH020.
- **What the version covers:** everything generated code binds to, since code generated under one version runs
  against the library of another once a referenced project was built earlier. That is the shape of the stamps, every
  member of `Typeshift.Generated`, `IStorable<TSelf>` and `Agent.Declaration`. A change to any of them raises
  `ReferenceReader.StampVersion`, so the reference built before it gets TSH020 at build time, where it would fail
  with a `MissingMethodException` when the app starts. `PublicApiSurfaceTests` keeps those members in
  `Snapshots/GeneratedContract.verified.txt` under the version, and fails when they change while it stays.
- **Where:** `Generated/Stamps.cs`, `Emitting/ModuleEmitter.cs`, `Parsing/ReferenceReader.cs`,
  `Repository/PublicApiSurfaceTests` (generator tests).

### D8. The generator targets Roslyn 5

- **Status:** approved.
- **Decision:** consumers need C# 14 for the extension blocks anyway, so the generator uses Roslyn 5 APIs, and v0's
  4.14 pin and its trap are gone.

### D61. The package targets .NET 11 only

- **Status:** approved.
- **Context:** .NET 11 shipped with C# 15's union types, and the owner asked for .NET 11 support, unions first.
  What a union needs from System.Text.Json, such as a case classifier on the serializer options, comes only with
  .NET 11.
- **Decision:** the library, its tests, the samples and the demos target `net11.0`, and the repository builds with
  the .NET 11 SDK. There is no build for .NET 10: the next LTS release, .NET 12, is where the package moves next. The
  generator stays on Roslyn 5.0 (D8), which every .NET 11 SDK exceeds. A project on another framework fails at
  restore with NU1202, which names `net11.0`, because the package's build files sit under that framework's folder.
- **Where:** `global.json`, the project files.
- **Alternatives:** a build for `net10.0` next to `net11.0` from the same source, which the owner declined, since .NET
  12 brings an LTS release again; `net10.0` only, referencing System.Text.Json 11 from NuGet, which moves every .NET 10
  app onto it.

### D9. Every project that declares toolsets or agents references the package

- **Status:** carried (v0 D9). TSH020 catches a reference without it.
- **Context:** v0 carried the rule without its reason. SDK 11.0.100 passes a package's analyzers and its
  `buildTransitive` props on to a project that reaches the package only through a project or another package. That
  is NuGet/Home#6279, open since 2017, in which analyzers ignore the default `PrivateAssets`, and no contract:
  `PrivateAssets="all"` or a reference to a compiled DLL stops the flow, and so would a fix.
- **Decision:** the rule stays, because a direct reference is the one setup that brings the generator whatever the
  reference looks like. The guide gives this reason and the flip side: while the flow lasts, a project that
  references yours gets XML docs on (D68) and a module (D55) too.
- **Alternatives:** one reference per solution, guarded by an app-over-library pair in
  `samples/Typeshift.Samples.PackageConsumer` that fails CI the day the SDK stops passing analyzers on; declined,
  since the docs would promise what NuGet documents otherwise.
- **Where:** `docs/guide/registration.md`, `src/Typeshift/PACKAGE.md`.

### D10. The hidden public surface generated code calls

- **Status:** carried (v0 D35).
- **Decision:** what only generated code calls is public and `[EditorBrowsable(Never)]`, in `Typeshift.Generated`.
- **Marked as generated:** every generated type, and every member generated into a class of the user's, carries
  `[GeneratedCode]`, with the generator's version, and `[ExcludeFromCodeCoverage]` (`Names.Generated`). The user's
  partial class never does: analyzers skip what the attribute marks, the Typeshift analyzer among them, and would
  skip the user's own code in it. The host's members and the tools' factories also carry `[DebuggerStepThrough]`.
  The version is the package's, without the commit a build appends, read once per process, so it keeps the
  generator's outputs cached.

### D11. The id type is an MSBuild property

- **Status:** owner; superseded by D72, which gives ids in code.
- **Was:** `<TypeshiftIdType>int</TypeshiftIdType>`, set once in `Directory.Build.props` and `Guid` by default, typed
  every settings and conversation id of the build. The generator wrote `global using` aliases over it
  (`ITypeshiftStore`, `ISettingsStore`, `IConversationStore`, `InMemoryTypeshiftStore`, `ConversationTracker`,
  `LiveRun`), gave the host members `settingsId` and `conversationId` parameters of it, closed the core services over
  it (`AddCoreServices<TId, TProviderId>`) and stamped it on each module. A reference built with another id type was
  TSH021, one the build could not find TSH029, and one less accessible than a public agent TSH006.
- **Now:** a host's ids come from `WithSettingsId` and `WithConversationId`, each typed by its call (D72). The
  provider id type stayed an MSBuild property, with its alias (D56), until D77 took it from each agent's `WithModel`.
- **Alternatives:** `AddTypeshift().WithIdType<int>()`, known only in the registering project, with generic run
  members checked at runtime; a generated interface per project, ambiguous under `InternalsVisibleTo`.

### D12. Typed host members are generated extension blocks

- **Status:** carried (v0 D4).
- **Decision:** `extension(AgentHost<TAgent> host)` per agent, in the agent's assembly, with `RunAsync`, `RunAsync<T>`,
  `RunStreamingAsync`, `ResumeAsync`, `ResumeStreamingAsync`, `AnswerAsync`, `AnswerStreamingAsync` (D80, at stamp
  version 8), `LoadSettingsAsync`, `SaveSettingsAsync`, `ConfigureAsync` and `ResolveOptionsAsync`. They call hidden
  library methods, so the logic lives and is tested in the library.
- **Namespace:** the blocks live in the global namespace, in a file of their own per agent, so every file has them in
  scope. In the agent's namespace, a file that named the agent in full or through an alias saw no typed members, and
  the owner asked why `RunAsync` was missing at times; the `Typeshift` namespace would have hidden them from a file
  that names `AgentHost` in full. Each class spells out the agent's full name, every part after its length, so two
  agents never share one, whatever their namespaces hold.
- **Tool lists:** the extension block on `ToolList<TToolset>`, which `Include(t => t.X)` picks from (D4), follows the
  same rule: the global namespace, a file of its own per toolset, a hidden class `TypeshiftTools_{full name}` with
  every part after its length. It lived in the toolset's namespace as `{Toolset}ToolList` first. There a pick was
  CS1061 in a file that named the toolset in full or through an alias, as the host members had been, and the class
  shared its name with any type of the namespace called `{Toolset}ToolList`, and with the list of a nested toolset
  of that flat name.

## Declarations

### D13. Tool names derive from the method

- **Status:** owner.
- **Decision:** `SearchTickets` is `search_tickets`; an `Async` suffix is dropped, so `CloseTicketAsync` is
  `close_ticket`. `WithName` overrides. The name keys stored settings, so renaming a method renames the tool: the docs
  say to pin the old name with `WithName` first. TSH007 also compares the generated property names. An agent's own
  tools are compared only across the handoffs of a conversation (D57).
- **Alternatives:** keeping `Async` (`close_ticket_async`), which leaks a C# convention to the model; a stored key
  separate from the name, a second identity.
- **Where:** `Parsing/ToolsetParser.cs`.

### D14. Descriptions come from XML docs

- **Status:** carried (v0), `WithDescription` overrides; parameters the model never sees need no `<param>` (D2's
  suppressor). An agent's summary describes it as a method's describes its tool, at the owner's request.

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
  `AddTypeshift()` of a project referencing it registers like any referenced module. A project that only holds a
  JSON context gets one in the same way (D45).
- **Consequences:** Weboost.SchemaAnnotations collects every documented type and property of the project, internal
  ones included, not only those a tool takes. Their docs are compiled into the assembly as text. And its generated
  file names each documented type, so one marked `[Obsolete]` is CS0618 there, an error where warnings are errors,
  and one marked `[Experimental]` an error in any build. `TypeshiftSchemaAnnotations=false` avoids both, and the fix
  belongs to Weboost.SchemaAnnotations (see [Status](./status.md#known-gaps)).
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
  bound by type, nullable or not, and left out of the schema; a parameter JSON cannot fill, such as `ref`, `out` or
  `dynamic`, is TSH004.

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
  toolset stamp and no registration, so no other agent offers them and `Include` and `Exclude` do not pick them.
- **As built:** the generator reads them with the toolsets' parser and writes their overrides, factories and functions
  into the agent, where the agent's `Settings`, its `Configure{Tool}()` methods and its declaration list them as they
  list a toolset's tools. The analyzer applies every tool rule. An own tool's name differs from the agent's toolsets'
  (TSH025) and from every tool of the agents its standing approvals reach or come from (TSH007), since an approval names
  its tool: handoffs pass the conversation on, directly or through other agents, and a sub-agent runs with its caller's
  approvals (D33) but without a conversation, so it hands off nothing. The module stamp lists each agent's tools, its
  own tools and those that require approval, at stamp version 5, so a handoff or sub-agent into a referenced project's
  agent is checked too; that agent's own uses are not stamped.
- **Where:** `Agents/Agent.cs`, `Parsing/ToolsetParser.cs`, `Parsing/AgentParser.cs`, `Parsing/Catalog.cs`,
  `Emitting/AgentEmitter.cs`.
- **Alternatives:** a second, lighter base class for small tasks, such as `AgentTask<TResult>` with instructions and
  a typed result, a second concept beside `Agent` (the owner's open question); `AIFunctionFactory` functions in
  `WithTools`, which reflect, skip the analyzer and settings, and need JSON metadata the generator never checks;
  own tools pickable from a `ToolList<TAgent>`, which would let one agent offer another's tools; own tools unique
  across the host like toolsets' tools, which made two agents' one-off tools clash; unique per agent alone, which lets a
  standing approval reach another agent's tool of the same name.

### D24. Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules

- **Status:** approved; superseded by D80, which makes what a run asks a stored record, and D81, which moves the
  pause below the tool loop.
- **Was:** `FunctionInvokingChatClient` ended a run with `ToolApprovalRequestContent`s for the calls of tools wrapped
  in `ApprovalRequiredAIFunction`, listed by `response.ApprovalRequests`. The host answered with
  `CreateResponse(approved)`, `ApproveForConversation()` or `ApprovePlan(mode)` in a message of the conversation's
  next run. A layer above the tool loop (`ApprovalChatClient`) answered the requests that rules and standing
  approvals decided and continued the loop, stored the requests and its own answers when a run ended asking, and
  read standing approvals from the stored answers and from a summary entry (`ConversationEntry.StandingApprovals`).
- **Now:** a call that waits for your app is an `ApprovalDecision` in the conversation's decisions document, answered
  by its id through `AnswerAsync` (D80). A gate below the tool loop decides each call, with the rules, the standing
  approvals and the mode, and the loop never pauses (D81). `RequiresApproval()` still wraps the tool in
  `ApprovalRequiredAIFunction`, which is how the gate knows it.
- **Alternatives:** an approval callback the run awaits, which holds a run open across requests.

## Runtime

### D25. A run uses the services of the scope its host was resolved from

- **Status:** owner.
- **Context:** v0 gave each tool call a fresh scope of the root container, so request-scoped services (the current
  customer) never reached tools, context or the chat client provider (audit finding 2).
- **Decision:** `AgentHost<TAgent>` is transient and keeps the provider it was resolved from, or the scope
  `TypeshiftRuntime.Agent<TAgent>()` created for it (D76). Tools, context providers, middleware, the MCP tool provider
  and the chat client provider resolve from it. Agents and toolsets stay singletons, the store too.
- **Alternatives:** a run-context tool parameter carrying the typed id, which built-in toolsets compiled before the
  host cannot type; per-call scopes, which lose the request.
- **Where:** `AgentHost.cs`.

### D26. Runs return Microsoft.Extensions.AI's types

- **Status:** owner.
- **Decision:** `RunAsync` returns `ChatResponse`, `RunAsync<T>` `ChatResponse<T>`, `RunStreamingAsync`
  `ChatResponseUpdate`s; messages carry the answering agent as `AuthorName`. Inputs are a `string` or a
  `ChatMessage` (for images). What a run asks your app comes back in the response as `DecisionRequestContent`s,
  listed by `response.Decisions`, and is answered through `AnswerAsync`, never in an input (D80, D81). The raw path (`ResolveOptionsAsync`, `CreateChatClientAsync`, the client)
  stays; options it resolved remember their settings id, so the conversation id is the only string.
- **Alternatives:** an own `AgentResponse`, a type to learn for what `ChatResponse` holds.

### D27. Four settings layers

- **Status:** brief. Code defaults, the agent's code, its stored `Settings`, the run. Stored tool
  defaults (`ToolDefaults`) are gone, so resolving a run's settings is one document. The stored layer holds the
  agent's own settings and, over them, those under the settings id (D66).

### D28. The model is a configurable option like any other

- **Status:** owner; changes v0 D34.
- **Decision:** `Configurable(o => o.ModelId)` puts `Model`, and its `Provider` (D77), into the agent's `Settings`,
  overriding `WithModel`; without it the model stays in code. The allow list holds for the model too. The options
  that may be configurable are the model, the instructions, the sampling options, the stop sequences, multiple tool
  calls and reasoning; the rest belong to the run or have no stored form (TSH012).
- **Alternatives:** `Model` in every `Settings` (v0, the brief's first sketch), which lets a settings form a
  customer edits pick any model the provider knows.

### D56. A model is a provider and a model, one interface serves both

- **Status:** owner; superseded by D77, which types providers per agent.
- **Was:** `WithModel(provider, model)` and `IChatClientProvider<TProviderId>`, as D77 keeps them, with one provider
  id type for the build: the MSBuild property `TypeshiftProviderIdType`, `string` by default, with a generated
  `IChatClientProvider` alias. The interceptor closed the core services over it (`AddCoreServices<TProviderId>`, which
  registered `ChatClients<TProviderId>`), and module stamps carried it. A provider of another type at `WithModel` or
  `WithChatClientProvider<T>()` was TSH030, a type the build could not find or settings could not store TSH029, and a
  reference with agents built with another type TSH021.
- **Now:** each agent's providers are of the type its `WithModel` names (D77).

### D77. Each agent's providers are of the type its WithModel names

- **Status:** owner; supersedes D56.
- **Context:** a model was one string the host's provider read, so a host with several providers invented composite
  names such as `lmstudio/qwen/qwen3-8b`, split them again, and wrapped each client to put the provider's own id back
  into the request. D56 split it into a provider and a model, of one provider id type per build, so an app could not
  run an agent of an enum's providers beside one of strings. As for ids (D72), the owner asked that the type move
  into code, and chose "Per agent, inferred".
- **Decision:** `WithModel(provider, model)`. `IChatClientProvider<TProviderId>` has one method,
  `GetChatClientAsync(provider, model, ct)`, resolved scoped from the run's services, and `ChatOptions.ModelId` carries
  the model alone. An agent's provider id type is the static type of the provider its `WithModel` takes, and
  `string` for an agent without one. The generator reads it, so that agent's `Settings.Provider` and
  `SummaryProvider` are of it, and the agent's runs ask the `IChatClientProvider<T>` of that type. The summary model
  is the run's model unless named, and its provider the run's provider: the agent's code names a summary model only,
  and only settings (`SummaryProvider`) name another provider for it. A summary model the run's provider cannot
  serve, as after a user switched the agent to another provider, costs a failed call and a warning, and the run's own
  model writes the summary (D32).
- **As built:** a provider goes down the run as a `ProviderId<TProviderId>`, made where its type is known: in
  `WithModel`, and in the generated `ApplySettings`, through `SetProvider<TProviderId>` and `SetSummaryProvider`. It
  reaches the provider of its type through a generic call, as a `StoreId<TId>` reaches its store (D72), so no type is
  made at run time and Native AOT keeps working; it is equal only to a provider of the same type and value. D63's
  fallbacks hold for any type, since keyed services take keys of any type. `WithChatClientProvider<T>()` registers `T`
  as each `IChatClientProvider<X>` it implements, of any `X`, as `WithStore<T>()` registers a store, and a `T` that
  implements none is TSH030. Settings that store a provider need a type they store as one value, a string, a number,
  a `Guid` or an enum: TSH029, on the provider. An agent whose settings store none may name a provider of any type.
  Settings that store one need a type the generated `Settings`, in a file of their own, can name, so no type nested
  as `private` or `protected` and no `file` type, and a public agent's settings a public one (TSH006); the
  generator writes `string` in place of a type it cannot name, so its code compiles. The `WithModel` that
  `new TAgent()` runs last types the providers, as the module creates an agent with its parameterless constructor:
  the last `WithModel` of that constructor, or else of the constructors it chains to with `this(...)`, nearest
  first. Gone with the build's type: `TypeshiftProviderIdType`, the
  `IChatClientProvider` alias, the type parameter of `AddCoreServices`, `ChatClients<TProviderId>`, the module
  stamps' provider id type (stamp version 6), TSH021, and the build-wide parts of TSH029 and TSH030.
- **Where:** `Hosting/ProviderId.cs`, `Hosting/ChatClients.cs`, `Hosting/IChatClientProvider.cs`,
  `Agents/ChatOptionsBuilder.cs`, `Generated/AgentDeclaration.cs`, `DependencyInjection/TypeshiftBuilder.cs`,
  `Compaction/Compaction.cs`, `Parsing/AgentParser.cs`, `Emitting/AgentEmitter.cs`, `Emitting/OptionMembers.cs`,
  `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** a model record with a request type, a client cache and a catalog, more surface than the problem
  needs; methods that list providers and their models, which Typeshift never calls; a summary provider in code, tied
  to a provider the user may switch away from; compaction that drops turns without a summary model. For the type:
  the MSBuild property, one type per build (D56); one type per app on the `AddTypeshift()` chain, known only in the
  project that registers; a type each agent declares beside `WithModel`, which says it twice; looking the provider
  up by its value's runtime type, which needs `MakeGenericType` and misses a provider whose runtime type differs from
  its declared one.

### D63. A registered chat client serves a run no IChatClientProvider serves

- **Status:** owner.
- **Context:** the owner asked that an agent without a model still run, and for a simpler setup than writing an
  `IChatClientProvider`: one provider, or a client per provider, for a demo or a local test.
- **Decision:** a run takes its client from the first of these that serves it:
  - the `IChatClientProvider` of the agent's provider type (D77), for a provider and a model;
  - an `IChatClient` registered under the agent's provider as its key, as Microsoft.Extensions.AI's
    `AddKeyedChatClient` registers one;
  - the `IChatClient` registered for every agent, as `AddChatClient` registers it.
  The provider decides alone for a run that names a provider and a model; when it throws, no client is tried after
  it. An agent that names no model runs on the last client, without a `ModelId`, so the client's own model answers.
  A model the agent names reaches a registered client as `ModelId`, as it reaches a provider's.
- **Where:** `Hosting/ChatClients.cs`, `Hosting/AgentHost.cs`, `Compaction/Compaction.cs`.
- **Alternatives:** chain calls of Typeshift's own, such as `WithChatClient(client)`, which repeat what
  Microsoft.Extensions.AI's registration already does; a built-in provider keyed by a registry of Typeshift's, a
  second place to register clients.

### D29. The chat client pipeline

- **Status:** approved.
- **Decision:** outermost first: conversation tracking (resume, run boundaries, settling the decisions that wait,
  live updates, the `invoke_agent` span), run middleware, context providers, the mode (the tools of the run's mode
  and each pass's share of the limit), `FunctionInvokingChatClient` (handoff, run options, the tool gate and tool
  middleware as its `FunctionInvoker`), the decision gate (D81), checkpoints (each step appended before compaction
  sees it; steering input and background answers injected here), unique call ids (D59), compaction,
  `OpenTelemetryChatClient`, the provider's client behind a wrapper that never disposes it.
  The gate, checkpoints and compaction sit inside the tool loop, so they act around every model call. The mode
  layer builds a `FunctionInvokingChatClient` per pass, whose iteration limit is that pass's, so calls on one client
  never share a limit.
  The layers below the conversation's run only inside a run it began: the tool loop taken out of the created client
  with `GetService` and called on its own throws at the gate, where it would store nothing and count no model call.
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
  cleared first (calls kept), then the oldest turns summarized, by the run's model unless one is named (D77).
  Instructions, the latest `write_todos` call and call/result pairs stay; the summary is stored as an entry later
  runs start from. Without reported usage, tokens are estimated from the text.
- **As built:** the estimate counts characters (four to a token) over the messages, the instructions and the tool
  schemas, calibrated by the tokens the provider reported for the run's last call against the estimate of that call
  as it was sent. A model response's entry keeps that estimate (`EstimatedInputTokens`) beside its usage, and a run's
  first call is calibrated by the conversation's last entry that holds both. A report without an estimate, of a run
  without a maximum or of an earlier version, calibrates nothing: the stored history keeps the results a run cleared,
  so an estimate of it is not what the provider counted, and a calibration by it let the next run's first call exceed
  the maximum up to four times. Only results the model already
  answered after are cleared, and none that is no longer than its placeholder, which would make the call longer and
  cost a value the model still uses. The placeholder asks the model to call the tool again only for a tool marked
  read-only (D52); for any other it says that the result was cleared and no more, since a repeated call would repeat
  what the tool did. The summary model reads the turns it summarizes as a transcript: each call with its
  arguments, and each result as its tool returned it, cut to 2,000 characters, also where clearing took it from the
  run's model. What a context provider added (D31), and what run middleware added to the messages it received (D30),
  is the pass's own, like the instructions: it stays in every call and counts toward the target, the summary model
  never reads it, and no summary stores it. A message middleware put in place of another is summarized like the
  message it stands for, since compaction sees no other; pinning it would leave that turn in no summary. A summary is
  best effort (owner): a summary call that fails is logged as a warning and recorded on the run's span, the run's own
  provider and model write the summary when another was named (D77), and when that fails too the call goes out with
  its results cleared and no summary, for the provider to answer or reject. Before, the failure ended the run ahead of
  its own model call, and every later run of the conversation, since each summarizes first. The run's cancellation
  still ends it. What compaction did holds for the rest of the run: the tool loop keeps the whole
  history, so each later call gets the same cleared results and summary. The summary entry holds the summary, the
  kept todo list and copies of the kept messages, with the run's mode and the background calls still working, so a
  later run loads only that entry and the ones after it (`LoadConversationSinceCompactionAsync`, which a store may
  override to leave the rest unread). Standing approvals are no part of it: they are in the conversation's decisions
  document, which no summary touches (D80). The passes steering input or background answers start share
  `MaxToolIterations`: each gets what the run has left. A call a rule, a standing approval or auto mode approves is
  a round like any other, since the gate decides it inside the loop (D81). Calls your app approves run in the
  conversation's next run, which counts its own rounds; carrying them out takes one turn of the loop that calls no
  model.
- **Where:** `Compaction/`, `Conversations/ConversationEntry.cs` for the stored estimate,
  `Conversations/ConversationRun.cs` for what a pass added, `Approval/ModeChatClient.cs` for the shared limit.
- **Alternatives:** MAF's strategy pipeline and trigger combinators, more concepts for the same two steps; a
  tokenizer dependency; message-count triggers, which ignore that one tool result may outweigh a hundred messages; a
  separate `WithCompaction()` chain next to the limits. For a summary call that fails: ending the run, as it did,
  which let a summary model that was down fail every later run of a conversation; refusing only a summary model the
  code named on a provider the user switched to, which covers one of the two causes and needs the generator to say
  which layer named the model. For what a pass added: leaving every message the conversation never stores out of the
  summary, which loses the turns whose messages run middleware replaced.

### D33. Sub-agents, in the foreground or the background

- **Status:** owner.
- **Decision:** `UseAgent<T>()` offers a tool named like the agent, with its description and one `input` string. A
  call runs it in a fresh context with the same settings id, the caller's mode, standing approvals and rules, and no
  conversation of its own, and returns its text, or its JSON with `.WithResult<T>()` (D65). `.InBackground()` makes the tool return at once; the answer reaches
  the calling run as a message before a later model call, and the run does not end while sub-agents work. A
  sub-agent cannot ask your app: in the default mode its calls that need approval and that nothing approves are
  rejected, and its model reads the rejection as the call's result (D81; TSH024 warns). Circles are TSH026.
- **As built:** the sub-agent's tool is a runtime `AIFunction` that builds the agent's `AgentHost<T>` over the
  caller's services through a factory `UseAgent<T>()` captured (D76), so no generated code is needed; it finds its caller's run through
  `FunctionInvokingChatClient.CurrentContext`. Background answers join the run through the steering queue, so
  every run has a `ConversationRun`, which stores only with a conversation; the run's end cancels the sub-agents
  still working and awaits them, so none outlives its run or the scope of a host disposed after it (D76), and the
  next run tells the model which ones it lost, after a crash, a cancellation or a failure,
  including the ones a summary left working. TSH023 and TSH024 check the agents a compilation uses, a reference's
  through its module stamp, which carries the agent's name, whether it has a description and the tools it offers that
  require approval; tool stamps carry `RequiresApproval` for that list. TSH026 checks the compilation's own agents,
  since assemblies cannot reference each other in a circle.
- **Where:** `Agents/SubAgentFunction.cs`, `Conversations/ConversationRun.cs`, `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** passing a sub-agent's decisions up to the caller's conversation, which pauses a parent run on a
  child's call and needs the child's state stored to resume (possible later); MAF's six background-task tools
  (start, wait, results, continue, clear), where one tool and an injected answer do the job; storing each sub-agent
  run as a conversation of its own, which needs ids the library may not create.

### D34. Handoff continues with the agent handed to

- **Status:** owner.
- **Decision:** `UseHandoff<T>()` adds a declaration-only tool `handoff_to_{name}`. Calling it ends the current
  agent's loop; the target answers in the same run with the whole conversation. The conversation's last entry names
  the agent that answers its next run, if it is reachable through the host agent's handoffs. Handoff needs a
  conversation; run settings apply to the host's agent only. A handoff called in a response of which a call waits
  for your app is not carried out: the model reads that it was not handed off, and the agent that asked keeps the
  conversation, so the answers continue with it (D81).
- **As built:** the conversation layer sees the declaration's call the tool loop passes out, stores its result as an
  entry of the agent handed to, and lets that agent resume the run; a run without a conversation does not offer
  handoffs, and a call of an agent it does not hand off to is answered by the tool loop as an unknown tool. Tool results
  after which the tool loop made no model call are stored before the run ends, a handoff's and a terminating tool's
  alike. The handing agent's run swaps its claim on the conversation (D50) for a claim of the agent handed to, and
  passes it to that agent's run, as a run passed on to the agent that answers the conversation is passed its claim;
  so no run starts between the two. A run that starts then waits, as for any run still loading, and is refused once
  the agent handed to began. The claim travels as an argument and in the run's options rather than in an
  `AsyncLocal`, which a value set before a stream's `yield` loses and code the agent's run calls would carry along.
- **Where:** `Conversations/ConversationChatClient.cs`, `Conversations/LiveRuns.cs`.

### D65. A sub-agent can answer in a type

- **Status:** owner.
- **Context:** the owner asked whether a sub-agent's answer can be held to a JSON type.
- **Decision:** `UseAgent<T>().WithResult<TResult>()` runs the sub-agent with the response format of
  `RunAsync<TResult>` (D62), reads the answer as `TResult` and hands the caller's model the result serialized again,
  so the model reads `TResult`'s JSON without a `data` wrapper. An answer that is no `TResult` fails the call, and
  the calling model reads that the sub-agent did not answer in the schema it was given, with what it answered (D53).
  In the background the answer goes the same way: its JSON arrives as the message, or the failure does. The chain runs
  at runtime, as `InBackground()` does, so the generator reads nothing new.
- **Where:** `Agents/AgentUse.cs`, `Agents/SubAgentResult.cs`, `Agents/SubAgentFunction.cs`,
  `Conversations/ConversationRun.cs`.
- **Alternatives:** the sub-agent's text passed on unchecked, which leaves the calling model to find out what went
  wrong; a schema on the tool's result, which no provider reads.

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
  Microsoft.Extensions.AI's `OpenTelemetryChatClient` writes the chat spans and the metrics: one per backend, created
  the first time a run reaches it and kept as long as the host (`InstrumentedClients`), which passes each call on to
  the client its run got. One per run, disposed with it, reset the metrics after every run, since the OpenTelemetry
  SDK ends the instruments of a disposed meter; one per client instance did the same for a provider that hands out a
  client per run or per tenant. The tool loop writes its `execute_tool` spans under the run's span, with that span's
  source. A store that fails records the exception on the run's span.
- **One per backend:** a backend is what the instrumentation reads once from a client's `ChatClientMetadata` and
  writes on every span and metric: the provider's name, the server's address and port, and the client's own model.
  That model names only the calls that name none, so it counts only for a run without a model, and the clients a
  provider makes per model share one. One per provider and model of the run, as before, kept an instrumented client,
  with its meter and four instruments, for every model name and provider id an app ever used, for ever where they
  come from data, and gave all the clients under one provider id the metadata of the first. No entry is removed or
  capped, since that would dispose a meter; the backends an app talks to bound them. Each is made once, under a
  `Lazy`, so two runs that reach a new backend together leave no second meter behind.
- **Streaming:** an async iterator resumes in its caller's context (dotnet/runtime#47802), so after a streaming run's
  first update its span was no longer the current one, and the later chat and tool spans hung under the caller's span
  or started traces of their own. The run makes its span current again before it asks for each update, as
  Microsoft.Extensions.AI's tool loop does; the caller's current span stays its own.
- **Tool arguments and results:** under an `invoke_agent` span the tool loop asks that span, not its client, whether
  to write them: it looks for the custom property `__EnableSensitiveData__`. The run's span carries it when the run's
  `OpenTelemetryChatClient` captures content (`EnableSensitiveData`, which the variable sets), so tool spans carry
  content exactly when chat spans do. The key is internal to Microsoft.Extensions.AI;
  `TelemetryTests.Given_Message_Capture_A_Run_Tags_The_Tool_Span_With_Arguments_And_Result` fails when it changes.
- **Where:** `Telemetry/Telemetry.cs`, `Telemetry/InstrumentedClients.cs`,
  `Conversations/ConversationChatClient.cs` (`GetStreamingResponseAsync`).

### D37. No built-in toolsets

- **Status:** owner.
- **Decision:** the library ships no tools of its own. An app declares the file, shell, web or to-do tools it needs
  in toolsets of its own, and plugin packages may bring such toolsets later.
- **Details:** up to 0.1.0-alpha.4, `Typeshift.Toolsets` held `TodoToolset`, `FileSystemToolset`, `ShellToolset` and
  `WebToolset`, with `IFileSystemRoot`, `IWebSearch` and `IWebFetch`. They were the library's riskiest code (a shell, a
  file system jail and a fetcher that must guard against requests to private addresses), every app shipped them, and
  the analyzer exempted their namespace from TSH007. Compaction still keeps the latest call of a tool named
  `write_todos` (D32), so an app's own to-do tool keeps its list.
- **Alternatives:** keeping them in the library; a `Typeshift.Toolsets` package in this repository, built like any
  plugin, which then needed TSH021 to accept a toolset-only package built with another provider id type.

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

### D38. A store of documents and a store of conversations

- **Status:** approved; split at the owner's request.
- **Decision:** `ISettingsStore<TId>`: `LoadAsync` and `SaveAsync` of documents as JSON, keyed by id and kind, with a
  version. `IConversationStore<TId>`: `LoadConversationAsync` and `AppendConversationAsync` of `ConversationEntry`s
  keyed by the conversation id; `LoadConversationSinceCompactionAsync`, which runs call and every store implements
  (D71). `IDecisionStore<TId>` keeps a conversation's decisions (D79), and `ITypeshiftStore<TId>` is all three in
  one. The library serializes the documents (each agent's `Settings`), so a store implements no generic method and
  handles strings and numbers only.
- **Details:** `WithStore<T>()` registers each interface `T` implements, so settings and conversations may live in
  two stores, each registered by its own call. Settings and conversations differ in shape and load: a few documents
  read once per run and saved with a version, against an append-only log written at every step. TSH028 reports a
  type that implements none of the store interfaces, of any id type (D72), and registration throws for the same types.
  Registration reads the interfaces of the store's declared type, which `DynamicallyAccessedMembers` keeps under
  trimming, a store your app created included.
- **Alternatives:** v0's load and save only, which rewrote a whole conversation per turn; a generic append for any kind,
  a concept with one user; `LoadAsync<TData>` and `SaveAsync<TData>` over `IStorable` documents (up to 0.1.0-alpha.3),
  which made every store call `TData.Kind`, `Serialize` and `Deserialize` itself; one document per settings id holding
  every agent's settings, where a save of one agent's settings would conflict with a save of another's (D41); one
  interface for both (up to 0.1.0-alpha.4), which made a store of conversations implement settings methods it does not
  keep; `WithSettingsStore<T>()` and `WithConversationStore<T>()`, two calls where the interfaces already say what a
  store keeps; renaming the conversation methods (`LoadAsync`, `AppendAsync`), which would clash with the settings
  methods in a class that implements both.

### D66. Each agent has settings of its own, under no id

- **Status:** owner.
- **Context:** the owner asked for "a configurable, storable agent settings record for a single agent", stored "keyed
  by the kind" alone, so that a simple app needs no settings ids.
- **Decision:** `IAgentSettingsStore` keeps a document per kind, with a version, and no id. `ConfigureAsync(configure)`,
  `LoadSettingsAsync()` and `SaveSettingsAsync(settings, loadedVersion)` are the members of D41 without the id. Every
  run of the agent applies its own settings, over its code and under the settings of a settings id, so they are the
  stored layer of an app without settings ids and the defaults of every id in one with them. `InMemoryTypeshiftStore`
  implements the interface, and `WithStore<T>()` registers it as it does the other two (D38). A run loads them only
  when a store of them is registered, so a store without them leaves runs as they were. Loading or saving them
  without one throws, naming `IAgentSettingsStore`. They have paths of their own down to the store, and the members
  that take an id throw on a null one, so a missing id never writes every id's defaults.
- **Where:** `Storage/IAgentSettingsStore.cs`, `Storage/StoreAccess.cs`, `Storage/StoreExtensions.cs`,
  `Storage/InMemoryTypeshiftStore.cs`, `Hosting/AgentHost.cs`, `Generated/AgentRuns.cs`,
  `Generated/ModuleRegistration.cs`, `DependencyInjection/TypeshiftBuilder.cs`, `Emitting/AgentEmitter.cs`,
  `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** the agent's own settings in place of a settings id's instead of under them, which leaves an app
  with ids no stored defaults; a reserved id, such as `Guid.Empty`, which not every id type has and a store cannot
  tell from a real one; two more methods on `ITypeshiftStore<TId>`, which every store implementing it would have to
  add; settings without an id on `ISettingsStore<TId>`, which ties them to an id type; a null settings id for them,
  which a missing id of a reference type would reach; members of other names, such as `LoadOwnSettingsAsync`, which
  keep `LoadSettingsAsync(default)` meaning an id but give one record two sets of names.

### D67. A store of agents' own settings in files

- **Status:** owner.
- **Context:** the owner asked for first-party support of simple setups; an app with one agent and no database
  still has to keep its settings across restarts. The owner then asked for a chain call that takes options, so that
  "the user does not have to instantiate a class owned component by hand", that "file settings must only be exposed
  under the file-store configuration", and that "a single store setting for the folder is enough".
- **Decision:** the file store keeps each agent's own settings (D66) in `<folder>/settings/<kind>.json`, as
  `{"kind": ..., "version": n, "settings": {...}}`, so the file reads and edits by hand. A save holds the kind's lock
  file, opened with `FileShare.None`, so other stores and processes wait; it checks the version, writes a file of its
  own beside the old one, flushes it to the disk and moves it over, so a load reads the old file or the new one whole.
  Loads share deletion with that move, which Windows needs. A kind's file name keeps ASCII letters, digits, `_` and
  `-` and escapes every other byte as `%XX`, so any kind maps to a file of the folder, one to one; a name Windows
  reserves is escaped too. The file names its kind, so a file system that ignores case and gives two kinds one file
  fails the load instead of handing one agent the other's settings. A file of another shape throws an
  `InvalidDataException` that names it and the shape: the read tests the JSON type of the root, the kind, the
  version and the settings before it reads them, since a file edited by hand may hold any of them as something else.
  A save waits for the lock at most 30 seconds on the app's clock and then throws a `TimeoutException` that names
  the lock file, and it says at Debug that it waits, so a program that hangs with the lock held fails the save
  instead of hanging it. The lock file stays between saves, since removing it would let a waiting save lock a file
  that is no longer the one in the folder. Under the lock, a save removes the files that saves of its kind left
  unfinished when their process died. `WithFileStore(configure)` and `FileStoreOptions` are the only
  public parts, and `Folder` the only option. It defaults to `typeshift` under `AppContext.BaseDirectory`, which a
  service finds whatever its current directory, resolves at registration, and a blank one throws there, naming the
  option. The settings get the fixed `settings` folder in it, so a store of other data can take a folder beside it. The
  container builds the internal `FileAgentSettingsStore` over that folder with the app's `TimeProvider`,
  through the registration `WithStore<T>()` uses, so the last call wins for each interface. `WithStore(store)`
  registers a store your code created, as each interface the instance implements, and the container leaves it
  undisposed.
- **Where:** `Storage/FileAgentSettingsStore.cs`, `Storage/FileStoreExtensions.cs`, `Storage/FileStoreOptions.cs`,
  `DependencyInjection/TypeshiftBuilder.cs`.
- **Alternatives:** one lock per store instance, which two instances or processes on one folder pass through, tearing
  the file; refusing kinds that are no plain file name, which fails every run of an agent named in another script;
  TSH022 ignoring case, which would reject kinds a case-sensitive store keeps apart;
  a file store of settings ids and conversations too, a database in files that SQLite already is;
  the folder from configuration, which ties the store to a configuration section the app may not have; one file for
  every agent, where a save of one agent's settings rewrites the others'; `WithFileSettings` and a public
  `FileSettingsOptions`, which exposed the settings outside the file store's configuration; a settings folder of its
  own option, relative to `Folder` or absolute, a second setting where one folder is enough; a relative default
  folder, which a service resolves against the system's folder; a public `FileAgentSettingsStore` registered with
  `WithStore(store)`, which made every app create a class of Typeshift's and hand it the clock itself.

### D68. The package turns XML docs on

- **Status:** owner.
- **Context:** the owner asked for less ceremony and sane defaults, and a project without
  `GenerateDocumentationFile` got tools without descriptions (TSH011), a step every tutorial reader had to take.
- **Decision:** the package's props turn `GenerateDocumentationFile` on when the project leaves it empty. NuGet
  imports the props before the project's own properties, so they cannot see what the project file is about to
  choose. They write `True` with a capital T, and the package's targets, imported after the project's properties,
  act only while that value stands. A project that sets the property, either way, keeps its choice and every
  warning that comes with it.
- **For a project that never chose,** the targets do three things, so that the docs the package turned on change
  nothing else. They add CS1591 to `NoWarn`, so the project gets no warning for each undocumented public member.
  They keep the compiler's other warnings about XML comments, such as a missing `<param>` or a stale `cref`, from
  becoming errors under `TreatWarningsAsErrors` or `-warnaserror`, because the compiler checks comments only with
  docs on and the project's comments were written without that check. And they leave the XML file in `obj`: it is
  not copied to the output folder, so no app publishes it, and it is taken out of the documentation output group,
  so the project's own package does not carry it.
- **As built:** the package consumer sample sets no `GenerateDocumentationFile`, checks a tool's description and the
  docs of a type it takes, and carries an undocumented public type and comments with two mistakes. CI publishes it
  and fails on an XML file, then builds it with docs of its own choice and expects CS1591.
- **Where:** `buildTransitive/Typeshift.props`, `buildTransitive/Typeshift.targets`,
  `samples/Typeshift.Samples.PackageConsumer`, `.github/workflows/ci.yml`.
- **Alternatives:** the tutorial's `Directory.Build.props`, a step of ceremony; docs on without hiding CS1591, which
  fails every build that treats warnings as errors; the `NoWarn` in the props, which hid CS1591 in every project
  that turned docs on in its project file; a test for an empty property in the targets, where the SDK has already
  set it to `false`; the XML file in the output folder, which every app that references the project would publish.

### D70. Instructions that never change are one call

- **Status:** owner.
- **Context:** the owner asked for less ceremony. Constant instructions took an override of `GetInstructions()`, three
  lines outside the constructor that holds everything else an agent declares.
- **Decision:** `WithInstructions(text)` in the constructor sets the instructions, and `GetInstructions()` returns
  them unless the agent overrides it. The call runs when the agent is built, as the chat options' calls do, so the
  text need not be a constant. An override still wins, for instructions built per run.
- **Where:** `Agents/Agent.cs`.
- **Alternatives:** `WithChatOptions().WithInstructions(...)`, beside the model, which would split the instructions
  between `ChatOptions` and the override; reading them from the agent's XML docs, which already describe it to other
  agents (D14).

### D71. Every conversation store implements the load from the last summary

- **Status:** owner.
- **Context:** the owner asked that the code, not only the docs, lead a store's author to the load runs call. It had a
  default on the interface that read the whole conversation, so a store that never knew of it read every entry on
  every run, however long compaction had let the conversation grow.
- **Decision:** `LoadConversationSinceCompactionAsync` has no default, so a store that leaves it out fails to build,
  and the IDE's fix inserts it with the XML docs that say what it returns. `entries.SinceLastSummary()`, an extension
  on the entries, is the one-line body of a store that can only read whole conversations.
- **Where:** `Storage/IConversationStore.cs`, `Storage/ConversationEntries.cs`, `Storage/InMemoryTypeshiftStore.cs`.
- **Alternatives:** the default with an analyzer warning on `WithStore<T>()`, which can be suppressed, and whose "does
  any agent compact" a compilation cannot always answer for agents of other projects; only the docs, which the owner
  found not enough; dropping `LoadConversationAsync` instead, which a chat UI needs for the whole history.

### D39. Kinds are stable names

- **Status:** owner.
- **Decision:** `IStorable.Kind`: for an agent's `Settings` its `WithStorageKind("...")` if it has one, or else its
  class name; `conversation_entry` for conversation entries. Moving an agent to another namespace keeps its data;
  renaming the class keeps it once `WithStorageKind` pins the old name. Two agents of one kind are TSH022.
- **Alternatives:** the agent's name, which would need unique names (out of scope); the full type name (v0).

### D40. A schema version per document

- **Status:** owner.
- **Decision:** `IStorable.SchemaVersion`, 1 for `Settings` until `WithSchemaVersion(n)` raises it. The JSON carries it
  as `"$schemaVersion"`, left out at 1, and `Deserialize` refuses a newer one; migrating older documents is the app's
  job, in its store or its database.
- **Details:** a store keeps the JSON and a version and nothing next to them. A schema version beside the JSON gave
  every store a column and every save a parameter, and a JSON library that read the JSON alone never saw it.
  `ConversationEntry` carries its schema version the same way, and `System.Text.Json` and Newtonsoft.Json check it too,
  where they used to read every entry as the current one. JSON written before the schema version moved names none and
  reads as 1; a `"$schemaVersion"` that is no whole number is refused, since it cannot be shown to be older.
- **Alternatives:** a hash of the record's shape (automatic, but no store can map it to a migration); a
  migration hook on the agent; an analyzer against a shipped list of stored members (later); the schema version next to
  the JSON (up to 0.1.0-alpha.4), a column every store kept; no schema version at all, which would read settings a newer
  release wrote after a rollback.

### D41. Optimistic concurrency

- **Status:** owner.
- **Decision:** `SaveAsync` takes the version the caller loaded as `loadedVersion`, 0 for none, and throws
  `StoreConflictException` when the stored one differs. `ConfigureAsync` reloads and reapplies until it wins (each
  conflict means another save won), at most ten times, so a store that conflicts every time fails instead of spinning.
  `SaveSettingsAsync` takes the version from `LoadSettingsAsync`.
- **Details:** one statement that checks and writes needs no exception handling and no second read. Before, the docs' EF
  Core store set a concurrency token, caught `DbUpdateConcurrencyException`, and after a failed insert read the row
  again to tell a conflict from another failure. The rule itself is in the store guide. The store's number is the
  version and the record's is the schema version (D40), so the two never share a name where a store meets both, as a
  migration in `LoadAsync` does. `loadedVersion` names the old version, the one the stored version must still equal;
  the store writes the new one and returns it.
- **Alternatives:** `SaveAsync` returning null for a lost save and Typeshift throwing, so a store never builds the
  exception, kept as the owner's open question since it breaks every store again; an opaque `string` version, which an
  ETag fits as is, open for the same reason; `revision` for the store's number (up to 0.1.0-alpha.4), which kept the
  plain word for the schema while store authors handle the store's number in every call; `version` for the parameter,
  which reads as the version to write; `expectedVersion`, which the owner found unclear.

### D58. A store's own tests check it against the contract

- **Status:** owner; the second id, the entries' times and the save of a document that is not stored came with the
  audit of November 2026.
- **Decision:** `store.VerifyAsync(id, otherId)`, an extension method on `ISettingsStore`, `IConversationStore`,
  `IDecisionStore` and `ITypeshiftStore`, runs the rules Typeshift relies on against a store and throws an `InvalidOperationException` that
  names the rule a store broke. It runs the rules of every interface the store implements for ids of that type,
  whichever one it is called through, so a store that is both cannot pass on one alone. It needs no test framework,
  so a store's tests in any framework can call it. It writes under both ids, as settings ids and as conversation ids,
  so tests pass two that nothing else uses. What it stores under the second id first must be as it was at the end, so
  a store that forgets the id in a load, in an update or in its search for the last summary fails; with one id such a
  store passed and went on to serve one customer's settings or conversation to another. A store that is also an
  `IAgentSettingsStore` is checked under kinds named after the id, from `typeshift_check_<id>` on, and for keeping
  its own documents apart from an id's; `VerifyAgentSettingsAsync(kind)` checks a store of them alone (D66). A call
  covers the interfaces of one id type, since the check casts the store with the type of the ids it is given and
  reads no interfaces by reflection, which trimming rules out. A store whose settings ids and conversation ids have
  two types (D72) takes `VerifyAsync(settingsId, otherSettingsId, conversationId, otherConversationId)`, which
  throws when the store keeps no conversations under ids of that type, or one call per id type.
- **Details:** the steps are on [Implementing a store](../guide/implementing-a-store.md#check-your-store). A store
  of decisions is checked under the conversation ids (D79). Entries carry fixed times, so the check reads no
  clock, and the times tie and run backwards, as real ones can after a clock step or between machines, so a store
  that orders a conversation by them fails; the check tells entries apart by their JSON. A save at a version other
  than 0 of a document that is not stored must conflict, as the save of a document deleted since it was loaded. A
  store's own exception where a conflict belongs is named in the message. A conversation id with entries, or a
  settings id that holds the check's own document, fails with a message that asks for a new id and names no rule,
  and two equal ids throw an `ArgumentException`.
- **Where:** `Storage/StoreExtensions.cs`.
- **Alternatives:** `StoreCheck.CheckSettingsAsync` and `CheckConversationsAsync`, two static methods named for the
  interface rather than called on the store, which the owner found clunky and which let a store that is both pass on
  one; one named case per rule for a `[Theory]`, which adds API and leans on a test framework; an abstract xUnit test
  class in a package of its own, which ties stores to one test framework; a checking decorator around the registered
  store, which finds a broken rule only when a run hits it; `VerifyAsync(id)` alone (up to 0.1.0-alpha.6) or kept
  beside the call with two ids, where the short call passes a store that mixes customers; a second id the check
  makes itself, which no id type of the app's choosing offers; new kinds per call for agents' own documents (up to
  0.1.0-alpha.6), which left documents behind under names nobody could predict; entries with rising times, which a
  store that orders by time passes.

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

- **Status:** carried (v0 D26), changed by D69. Once a store is registered, everything that loads or saves through a
  store interface none of the registered stores implements throws, naming `WithStore<T>()` and the interface (D38).
  Without any store, D69 keeps everything in memory. A load of decisions is the exception: without a store of them it
  answers "none", and only a write throws (D79).

### D69. Without a store, everything is kept in memory

- **Status:** owner.
- **Context:** the owner asked for sane defaults, "for stock users to have a working library at their hands from the
  get-go". Without `WithStore<T>()`, every settings save, every run with an id and every approval threw.
- **Decision:** when the container holds no store, the store access keeps everything in an `InMemoryTypeshiftStore`
  of its own per id type (D72), a singleton like the host's other services. A store is any registration, without a
  key, of `IAgentSettingsStore` or of an `ISettingsStore<>`, `IConversationStore<>`, `IDecisionStore<>` or
  `ITypeshiftStore<>` of any id type, whether `WithStore<T>()` made it or your app did. `AddCoreServices` hands the store access the service
  collection, which it reads once, at the first store call, comparing each service type by its generic definition so
  no type is made at run time. The first save or append logs a warning, once, that names `WithStore<T>()`, and
  `WithFileStore()` for agents' own settings, and says the data is gone when the app stops; loads stay quiet, since
  a load of nothing loses nothing. Once any store is registered, only registered stores serve, so an app that kept
  settings in a database and forgot a store of conversations still gets the exception of D43 instead of
  conversations that vanish. A store registration that is no singleton fails the first store call, naming its
  interface, its lifetime and `WithStore<T>()`: the store access asks the root (D25), which refuses a scoped service
  where scopes are validated and elsewhere hands out one instance for every request, with whatever scoped services
  it took. The store access reads the registrations before it asks the container, so the message is Typeshift's
  either way. Its lookup of the agent settings store keeps no failed creation, so a store whose database was down at
  the first run is asked for again by the next.
- **Where:** `Storage/StoreAccess.cs`, `Generated/ModuleRegistration.cs`.
- **Alternatives:** throwing as before, ceremony every first run hits; memory for each interface no registered store
  implements, which hides a forgotten store in an app that chose one; a warning at startup, which an app that never
  stores anything would see too; asking the container for the stores of the build's id type, which went with that
  type (D72); `WithStore<T>()` alone as the switch, which kept the settings of an app that registered its store of
  conversations itself in memory, under a warning that no store was registered; resolving a scoped store from the
  run's scope, which gives up the one instance that D25 and the store guide plan for, a second way to write a store.

### D79. Decisions have a store of their own, and a setup that would lose them throws

- **Status:** owner.
- **Context:** what a run asks your app (an approval, a plan) becomes a record with a state, in one document per
  conversation, which answering and carrying out change by compare-and-set (D80). The document needs a home with a
  version check, and losing it loses the answer to "did this approved call start".
- **Decision:** `IDecisionStore<TId>`: `LoadDecisionsAsync(conversation)` and
  `SaveDecisionsAsync(conversation, json, loadedVersion)`, one JSON document per conversation, versioned exactly as a
  settings document (D41). `ITypeshiftStore<TId>` includes it, `WithStore<T>()` registers it like every other store
  interface a class implements, `InMemoryTypeshiftStore<TId>` implements it, and `VerifyAsync` runs the version rules
  against it (D58). With no store at all, the memory of D69 keeps decisions too. With stores registered and none of
  decisions for the conversation's id type, a load answers "none" without a store call, so an agent that never asks
  pays nothing, and the first write throws, naming `IDecisionStore<TId>` and `WithStore<T>()`. Nothing is kept in
  memory beside a real store.
- **Where:** `Storage/IDecisionStore.cs`, `Storage/StoreAccess.cs`, `Storage/StoreId.cs`,
  `Storage/StoreExtensions.cs`.
- **Alternatives:** a kind of their own on `ISettingsStore<TId>`, which needs nothing new from a full store but
  shares the settings' id space: settings ids and conversation ids are separate, often of different types, so
  conversation 7's decisions would sit among customer 7's settings, where a delete or a tenant filter meant for
  settings reaches them; both, through an adapter the app opts into, a second way to do one thing; memory for
  decisions beside a registered conversation store, which loses what a run asked on every restart while the
  conversation stays.

### D72. Ids are given in code, and their types pick the stores

- **Status:** owner.
- **Context:** the owner asked that the id type move from the build into code and become "more granular": settings
  and conversations of one app may have ids of different types. One MSBuild property typed every id of the build
  (D11).
- **Decision:** `host.WithSettingsId(id)` and `host.WithConversationId(id)` return a copy of the host that carries the
  id, typed by the call, so `WithSettingsId(42)` names an `ISettingsStore<int>` and `WithConversationId(chat)` an
  `IConversationStore<Guid>`. The id goes down the run as a `StoreId<TId>`, which reaches the store of its type
  through a generic call, so no type is made at run time and Native AOT keeps working; it is equal only to an id of
  the same type and value, so conversation 5 as an `int` and as a `long` are two conversations. The generated members
  take no ids: a run uses the host's, a resume resumes the host's conversation, and the settings members of a host
  with a settings id load, save and configure the id's settings in place of the agent's own (D66). `WithStore<T>()`
  registers a store as each store interface its declared type implements, of any id type, and TSH028 reports only a
  type that is no store. Without any store, D69 keeps one store in memory per id type, and warns on the first write
  that one of them takes. A copy runs in its host's scope and owns none of it (D76): disposing a copy leaves the scope
  alone, so the copy works as long as the host it came from, and only a host the runtime created disposes the scope,
  once.
- **The raw path:** `ResolveOptionsAsync()` hands the host's conversation on with the options. On a host without one,
  `ChatOptions.ConversationId`, a string, names a conversation in the `IConversationStore<string>`, the owner's
  choice: the raw path has no type of its own to parse it into.
- **The tracker:** `ConversationTracker<TId>` lists, watches and steers the conversations under ids of `TId`, and the
  container has one for each id type an app asks for, as the owner chose ("Inject `ConversationTracker<int>` etc.; the
  container provides one per id type you ask for"). `AddCoreServices` registers the open generic, whose public
  constructor takes the `TypeshiftRuntime`. Under Native AOT, `Microsoft.Extensions.DependencyInjection` refuses to
  close an open generic over a value type, so `WithConversationTracker<TId>()` registers the tracker of one type as a
  singleton of its own; the console sample resolves `ConversationTracker<Guid>` through it, and that of `string`
  through the open generic, published as Native AOT.
- **As built:** the plan's three steps are done. Ids came into code beside the build's id type, the docs, samples and
  demos moved to them, and then the build's id type went: `TypeshiftIdType`, the aliases of the id-carrying types, the
  id parameters and overloads of the generated members, `AddCoreServices`' id type, the id type of the module stamps
  (stamp version 5), and the id-type parts of TSH006, TSH021 and TSH029. A new conversation's default id is a `Guid`
  (D73).
- **Where:** `Hosting/AgentHost.cs`, `Storage/StoreId.cs`, `Storage/StoreAccess.cs`, `Generated/AgentRuns.cs`,
  `Generated/ModuleRegistration.cs`, `DependencyInjection/TypeshiftBuilder.cs`, `Conversations/ConversationTracker.cs`,
  `Conversations/ConversationChatClient.cs`, `Emitting/AgentEmitter.cs`, `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** an assembly attribute or two MSBuild properties, still one id type per build and outside code;
  `ForSettings(id)` and `InConversation(id)`, names the owner turned down; ids as parameters of every generic run
  member, which repeats them on every call and every overload; looking the store up by the id's runtime type, which
  needs `MakeGenericType` and misses an id whose runtime type differs from its declared one; copies that share the
  scope's ownership with their host, by a count, which keep it open while any copy lives and make each copy one more
  thing to dispose; one tracker of the build's id type with generic overloads for other id types, the interim design,
  whose `Runs` listed only the build's; registering the trackers of common id types up front, a list nobody chose.

### D73. Every conversation can be kept

- **Status:** owner.
- **Context:** the owner asked for an option, "if enabled by the user in the global settings", that "automatically
  generates conversation IDs when none was provided and stores them automatically", under a general configuration
  prefix, and accepted `WithOptions(options => options.PersistAllConversations = true)`.
- **Decision:** `TypeshiftOptions` holds what every run does, set by `WithOptions(configure)`, whose calls change one
  instance. With `PersistAllConversations`, a run of the host's own that has input, no conversation, no
  `ChatOptions.ConversationId` and no caller starts one: the conversation client gives a copy of the options a new id,
  so options the app reuses never share it. A run without input continues an interrupted run (D50), which a new
  conversation lacks, so it starts none, and neither does a raw-path call with options the app made, or none, since they
  carry no host's run. New ids come from `NewConversationIds(create)`, typed like any id (D72), which concurrent runs
  call at once, so it must be thread-safe and never repeat an id. Without it, a new id is a `Guid` of version 7 from
  the injected clock, so it needs an `IConversationStore<Guid>`, or no store at all (D69); an app whose stores keep
  none gets the `InvalidOperationException` of D43, which names `NewConversationIds` too. The raw path names its
  conversation in `ChatOptions.ConversationId`, a string (D72), so a conversation a raw call starts is under a string,
  the id of `NewConversationIds` when it makes strings and the `Guid`'s text otherwise, and the response's id names it
  again. The options of a run of the host's members say so (`ResolvedChatOptions.HostRun`), and only those get the
  typed id, which the app continues with `WithConversationId`. A sub-agent's run belongs to its caller's conversation
  and starts none, and a handoff continues the caller's. Every run with a conversation names it in
  `ChatResponse.ConversationId`, and each streaming update in a copy, since the tool loop still holds the update and
  reads an update that names a conversation as one the service keeps, which would leave the history out of its next
  call.
- **Where:** `DependencyInjection/TypeshiftOptions.cs`, `DependencyInjection/TypeshiftBuilder.cs`,
  `Conversations/ConversationChatClient.cs`, `Hosting/ResolvedChatOptions.cs`, `Hosting/AgentHost.cs`,
  `Storage/StoreAccess.cs`.
- **Alternatives:** `WithStoredConversations` or `TrackAll`, names the owner turned down for one under a general prefix;
  `Guid.NewGuid()`, which does not sort by time; ids of the build's id type, a `Guid` or its text for `string`, which
  went with the build's id type (D72); the id type of the registered conversation store, which an app with stores of
  several id types cannot pick; a `Guid` on the raw path too, whose text in `ChatOptions.ConversationId` then named
  another conversation, under a string id, and lost the history; a string for every new id, which would move the
  conversations of typed runs out of an app's `IConversationStore<Guid>`; reading an id type by reflection, which Native
  AOT warns about; ids from the store, which would add a method every store must implement; naming the new conversation
  only in a header or a property bag, which a typed run's response would hide.

### D74. Live updates are on by default

- **Status:** owner.
- **Context:** the owner asked to "ship with sane defaults, for stock users to have a working library at their hands
  from the get-go", and chose live updates on by default. `WatchAsync` threw until `WithRealtimeTracking()` was called.
- **Decision:** `AddTypeshift()` registers the default `RealtimeTrackingOptions`, so runs with a conversation publish
  their updates and `WatchAsync` reads them without a call. `WithRealtimeTracking(options)` replaces the options and
  only tunes the intervals and limits. A publish with no watcher is one dictionary lookup, and merged text waits for
  the next update, so no timer runs (D47).
- **Where:** `Generated/ModuleRegistration.cs`, `DependencyInjection/TypeshiftBuilder.cs`,
  `Conversations/ConversationTracker.cs`.
- **Alternatives:** keeping it opt-in, which the owner turned down; a switch to turn it off, which nothing needs yet.

### D75. An app without a container creates a TypeshiftRuntime

- **Status:** owner.
- **Context:** the owner asked for less ceremony, "Wolverine-style", and chose `TypeshiftRuntime.Create`, first named
  `TypeshiftApp`, over a factory on each agent. A first run took a `ServiceCollection`, `AddTypeshift()`, a provider,
  a scope and a host from it.
- **Decision:** `TypeshiftRuntime.Create(client, configure)` builds a container and returns its runtime, which owns
  it, and `runtime.Agent<TAgent>()` returns a host in a scope of its own (D76). The generator intercepts `Create` as
  it does `AddTypeshift()` (D1): the interceptor builds a `ServiceCollection`, registers the core services and the
  modules with its own `AddTypeshift`, and `ModuleRegistration.CreateRuntime` registers the client as an instance for
  every agent (D63), runs `configure` on the chain and builds the provider with scope validation. The container never
  disposes an instance, so the client outlives the runtime. The generator reads only a `Create` whose receiver names
  `TypeshiftRuntime` and skips a call bound at run time, such as one with a `dynamic` argument; a call it did not
  intercept throws, saying how to call it. The generator's pass binds the call to its sole candidate when the
  `configure` lambda names code it has not written yet. The library references
  `Microsoft.Extensions.DependencyInjection`, the container, which it did not before.
- **Where:** `Hosting/TypeshiftRuntime.cs`, `Generated/ModuleRegistration.cs`, `Parsing/InterceptParser.cs`,
  `Emitting/InterceptorEmitter.cs`.
- **Alternatives:** a generated `Create` on each agent, which the owner turned down, and whose agents would not share
  a store or a tracker; a library `Create` that registers nothing, since only the generator knows the project's
  modules; a container of the library's own, which would serve neither scoped services nor the app's;
  `AddChatClient(client)`, whose factory makes the container own the client and dispose it with the runtime.

### D76. The runtime is the root, and its hosts are units of work

- **Status:** owner.
- **Context:** D75's first design gave the app one scope for its lifetime, so every run shared its scoped services,
  and an app with a container still built a scope by hand for each background job. The owner asked for one root in
  both kinds of app, after Wolverine's `WolverineRuntime`, which creates short-lived `IMessageBus` instances, and
  renamed `TypeshiftApp` to `TypeshiftRuntime`.
- **Decision:** `AddCoreServices` registers `TypeshiftRuntime` as a singleton over the root provider, so a singleton
  of the app, such as a background service, injects it. `Create` resolves that singleton from the container it builds
  and hands it the container. Disposing that runtime disposes the container, which disposes the runtime again, and the
  second call finds nothing left to dispose. A runtime your app's container created owns nothing, and disposing it
  does nothing. `runtime.Agent<TAgent>()` creates an async scope and a host that owns it: one unit of work, whose runs
  use that scope's services as D25 has every host use its scope's. `AgentHost<TAgent>` is `IAsyncDisposable` and
  `IDisposable`. Disposing a host the runtime created disposes its scope; disposing one the container resolved, such
  as one injected into a request, does nothing, since the container owns that scope, and so does disposing a copy from
  `WithSettingsId` or `WithConversationId` (D72), which shares its host's scope. A scope keeps every disposable
  the container builds until it ends, so the library builds the hosts of sub-agents and handoffs itself
  (`AgentHost<TAgent>.In`) rather than resolving them, or a long-lived host would collect one for each run.
  `runtime.Services` is the root provider, for singletons such as a `ConversationTracker<TId>`. A request still injects
  `AgentHost<TAgent>` from its own scope. Your app lets a host's runs and streams finish before it disposes the host,
  and a run that ends awaits the background sub-agents it stopped (D33), so none works on in a disposed scope.
- **Where:** `Hosting/TypeshiftRuntime.cs`, `Hosting/AgentHost.cs`, `Agents/Agent.cs`,
  `Generated/ModuleRegistration.cs`.
- **Alternatives:** a separate `AgentScope` object that holds a scope and hands out hosts, which the owner turned
  down; one scope for the runtime's lifetime, D75's first design, whose scoped services, such as the chat client
  provider and what tools resolve, every run shared, concurrent ones included.

### D44. Settings records serialize themselves

- **Status:** carried (v0 D27), extended.
- **Decision:** member by member through hidden helpers, no reflection; camelCase, tools under `"tools"` by name, nulls
  left out, the schema version as `"$schemaVersion"` above 1 (D40). The generator also writes a `JsonConverter` on the
  record, so `System.Text.Json` (an ASP.NET endpoint) writes the stored shape too. `ConversationEntry` carries the same
  converter, and a `TypeConverter` to `string` that Newtonsoft.Json writes the stored JSON through, so a store hands
  entries to either library as they are.
- **Details:** each overrides record writes and reads itself (`IJsonStorable<T>`), so an agent's `Settings` in
  another assembly than the toolset delegates to it. A member that does not read, missing or mistyped, reads as
  null and keeps the layer below; a document that is no JSON object throws. Enums are stored by name, so reordering
  an enum keeps them. A value without a name, such as `[Flags]` with none set, a combination with a bit no member
  names, or a cast number, is stored as its number, in digits whatever the culture, and as those digits where it is
  a dictionary's key. A string of digits reads as the number too, which is how versions up to 0.1.0-alpha.6 wrote
  such a value before reading it back as absent. Non-finite numbers are stored as strings. The writer keeps text
  outside ASCII as it is (`JavaScriptEncoder.Create(UnicodeRanges.All)`), for settings, for conversation entries and
  for the file store's files: a default writer escaped every such character as `\uXXXX`, which doubled the bytes of
  Chinese, Japanese and Korean text and hid it from a database's search. Quotes, `<`, `>`, `&` and characters beyond
  the Basic Multilingual Plane stay escaped, since an API may pass stored JSON on to a page as it is; the relaxed
  encoder, which leaves them, lost for that reason. Readers take either form, so JSON stored before reads as it did.

### D78. Stored contents follow Microsoft.Extensions.AI's content contract

- **Status:** from the audit of November 2026; in force until the owner decides otherwise.
- **Context:** an entry's messages are written and read through `AIJsonUtilities.DefaultOptions`, a closed
  polymorphic contract. A response with one content of a type outside it, such as a provider client's own, failed
  the append of its whole step, which D46 logs and passes over, so the answer was missing from the conversation.
  One stored `$type` the installed version cannot read, after a package upgrade or downgrade, made every load of
  the conversation throw, for runs and for the app's own reads.
- **Decision:** the JSON of an entry's messages is Microsoft.Extensions.AI's, and what that contract cannot carry is
  left out, so it costs one content and neither the step nor the conversation. A run stores a step without the
  contents whose type the contract does not write and logs a warning that names the type, once per type and run.
  `ConversationEntry.Deserialize` reads a message whose contents do not all read content by content and leaves out
  those that fail, and a message left without any. The entry counts them, and a run that loads the conversation logs
  the count, once per load.
- **Details:** the types the contract writes are read once from its `PolymorphismOptions`, so the check serializes
  nothing twice. `Serialize()` still throws for such a content, so an app that builds entries of its own hears of
  it. `$schemaVersion` cannot guard this (D40), since the content JSON is not Typeshift's.
- **Where:** `Conversations/ConversationEntry.cs`, `Conversations/ConversationRun.cs`,
  `Conversations/ConversationChatClient.cs`.
- **Alternatives:** failing the load, as before, where one renamed experimental type leaves a conversation nobody can
  open; keeping an unknown content as raw JSON, which needs a content type of Typeshift's own that providers would
  then receive; dropping the whole message, which loses the text beside the content; serving an app's own content
  types from its JSON contexts, a later step once an app needs it.

### D45. Tool JSON: contexts from the modules, reflection outside AOT

- **Status:** approved.
- **Decision:** modules register the `JsonSerializerContext`s that list their tools' types (v0 D44). Outside Native
  AOT, Microsoft.Extensions.AI's reflection-based defaults cover the rest, so tools need no context; under AOT
  (`PublishAot` or `IsAotCompatible`), a type no context lists is TSH027.
- **As built:** TSH027 knows the types Microsoft.Extensions.AI's own context lists (strings, numbers, `bool`, dates,
  `TimeSpan`, JSON elements) and the contexts of the compilation; a tool returning `IAsyncEnumerable<T>` returns a
  `List<T>`, which it checks. The console sample publishes as Native AOT in CI, where a trim or AOT warning fails the
  build.
- **Which contexts:** the module names a context and reads its static `Default`, so `Discovery.CanRegister` takes a
  context the assembly can name that is partial and carries a `[JsonSerializable]`, for which System.Text.Json's
  generator writes the member where this generator cannot see it, or that declares a static `Default` itself. A
  context written by hand without one was CS0117 in the module. TSH027 counts the same contexts. An assembly that
  holds a context and no toolset, agent or schema annotations class gets a module that registers just the context,
  as D55's docs-only module; without it an app that keeps its agents in a library and turns XML docs off registered
  no context and failed at its first typed run under Native AOT. The library itself, which builds with the generator
  and registers its own context, gets none.
- **Streams are read by the library:** the generated function hands the stream to the hidden
  `GeneratedToolFunction.GetResultAsync`, which collects the list. Generated code called
  `System.Linq.AsyncEnumerable.ToListAsync` once, a name that exists twice in a consumer whose packages bring
  System.Linq.Async before 7.0, so the call was CS0433 in a file the consumer cannot edit. The library compiles
  against the framework alone, where no reference of the consumer makes a name ambiguous. It also checks the call's
  token before each item, since an iterator without `[EnumeratorCancellation]` never sees the token.
- **Where:** `Analysis/TypeshiftAnalyzer.cs`, `Parsing/Discovery.cs`, `TypeshiftGenerator.cs` (`CreateModule`),
  `Generated/GeneratedToolFunction.cs`, `Emitting/ToolsetEmitter.cs`, `samples/Typeshift.Samples.Console`.

### D62. Union types in tools and typed runs

- **Status:** approved.
- **Context:** .NET 11 serializes a C# 15 union as its case, with no discriminator. Three things kept unions from
  working in tools and typed runs. The web defaults read numbers from strings, so a union of a number and a string
  could not read a string. Two object cases needed a `[JsonUnion]` classifier. And the exported schema names the type
  the cases share once, on the union, so Anthropic's client rejected each case as having no type, and a typed run of
  an object union sent no schema at all.
- **Decision:**
  - The tool JSON options carry a union classifier. A JSON token picks its case as if numbers were
    strict first, then as the web defaults read it, so `"7"` still reads as a number when no case takes a string. An
    object picks the case whose properties it has, through System.Text.Json's structural classifier. A value that
    fits no single case fails with a message naming the cases, which the model reads (D53). A union's own
    `[JsonUnion]` classifier wins, and a union neither pass can tell apart keeps the serializer's classification.
  - Every schema `ToolJson` builds copies a union's type onto each case that names none, and a nullable parameter
    widens each case as well (D48). Polymorphic types get the same.
  - A typed run builds its answer's schema as a tool's parameters are built, XML docs included, and holds anything
    but an object with properties under `data`, as Microsoft.Extensions.AI holds a number. A contract of its own for
    `T` reads the answer from under `data`, since a converter in the options loses to an enum's converter and to a
    polymorphic type's metadata.
  - A schema node the serializer writes for a type it cannot handle, `"Unsupported .NET type"` with `not: true`,
    throws a `NotSupportedException` that names the member, since a provider rejects the node far from its cause, as
    the owner's `Exception` member showed.
- **Where:** `Generated/UnionCaseClassifier.cs`, `Generated/ToolJson.cs`, `Hosting/AgentHost.cs`.
- **Alternatives:** a `[JsonUnion(TypeClassifier = ...)]` on every union, code each app writes; strict numbers
  everywhere, which fails a model that sends `"5"` for a number; leaving the typed run's schema to
  Microsoft.Extensions.AI, which neither types the cases nor holds an object union under `data`.

### D64. A typed run can stream

- **Status:** owner.
- **Context:** the owner asked for a strongly typed answer from a streaming run.
- **Decision:** `RunStreamingAsync<TResult>` asks for the same response format as `RunAsync<TResult>` (D62) and
  returns a `StreamingRun<TResult>`. It streams the updates as `RunStreamingAsync` does and, once they are read to
  the end, holds a `ChatResponse<TResult>` in `Response`. It calls the model when it is read and only once, since
  reading it again would run the agent again. Both typed runs take a `ChatMessage` as well, so the run that answers
  an approval asks for the format again. The format is the run's, not the agent's: an agent handed the run off to, or
  answering in a handed-off conversation, answers in it, and a sub-agent answers in its own.
- **Where:** `Hosting/StreamingRun.cs`, `Hosting/AgentHost.cs`, `Hosting/ResolvedChatOptions.cs`,
  `Conversations/ConversationChatClient.cs`, `Generated/AgentRuns.cs`, `Emitting/AgentEmitter.cs`.
- **Alternatives:** partial objects parsed from the JSON as it grows, which needs a parser that reads incomplete JSON
  and gives up the types System.Text.Json checks; a list of updates the caller collects and a helper that reads the
  result from them, two calls where one serves.

## Conversations

### D46. Tracking, carried and fixed

- **Status:** carried (v0 D18), with the audit's fixes.
- **Decision:** tracking wraps the tool loop and marks where a run begins and ends; checkpoints inside it store
  each step (D50). The provider never receives the
  conversation id. On resume, each result follows the message that made its call, and unanswered calls, results
  without their call and error messages are left out, and so are the approval requests and answers a version before
  D80 stored. A failed save is logged and traced, and the answer returns; only the response that asks your app must
  be stored, since a decision without its call could not be carried out (D81). The agent's name goes on
  `AuthorName`, never into `AdditionalProperties`.
- **Details:** the end of a run is an entry of its own (`EndsRun`), with an `ErrorContent` when the run failed, was
  cancelled or its stream was left early. Calls and results pair by call id, which D59 keeps unique in a conversation;
  of a call or a result a store holds twice, the last copy counts. The checkpoints know what they stored by reference, since the tool loop
  hands the same messages on; a streamed response is stored from the updates, and the tool loop's own messages
  built from those updates are matched by count.
- **Where:** `Conversations/ConversationChatClient.cs`, `Conversations/CheckpointChatClient.cs`,
  `Conversations/ConversationHistory.cs`.

### D47. Live updates are a feed per conversation

- **Status:** approved.
- **Decision:** runs publish their updates from the start (D74), and `WithRealtimeTracking(options)` tunes them;
  `ConversationTracker.WatchAsync(conversation)` reads a bounded feed that drops its oldest updates when the watcher falls behind. A run never waits
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
  `ResumeAsync()` (and `ResumeStreamingAsync`) on the conversation's host continues it without new input, leaving
  out a call that was in flight; a new run on the conversation also starts from its steps. Resuming also carries
  out a batch of decisions your app answered, and while decisions are still open it calls no model and returns them
  (D80, D81). One run per conversation at a time in a
  process, claimed before the run loads the conversation, so no run reads it while another still stores its end. A run
  that finds the conversation claimed by a run still loading it waits until that run began or let go; one that finds a
  run going on, or ending, throws. Across processes it is the host's to ensure. Resuming a conversation whose last run
  ended and that waits for nothing throws.
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
  handoff. Steering input a run takes after it asked your app expires what it asked, as a new message does (D81). A
  background agent's answer expires nothing and starts no model call while a batch is open: it is stored, and the
  run that carries the answers out reads it.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/ConversationRun.cs`.
- **Alternatives:** a steering queue in the store, which works across processes but reads the store before every
  model call; queuing input for after the run, which is just the next run.

### D52. Plan and auto modes are built in

- **Status:** owner.
- **Decision:** `AgentMode` `Default`, `Plan`, `Auto`, a run option (`WithMode`, configurable like any run option).
  `Plan` offers only tools marked `ReadOnly()` (toolset tools, or MCP servers with `UseMcpServer(...).ReadOnly()`),
  and a built-in `exit_plan_mode(plan)`, which asks your app with a `PlanDecision`; `plan.Approve(mode)` continues in
  `Default` or `Auto`, a rejection's reason sends the model back to planning. Outside a host's run in plan mode a
  call of `exit_plan_mode` asks nobody: the model reads that it is not planning, so no model can raise a plan prompt
  in a conversation that never planned (D81). `Auto` runs tools that require approval unless a
  rule rejects the call. A conversation keeps its mode: a run without one continues in the mode the last run ended in,
  stored on the run's ending entry and on a summary; a run whose settings name a mode switches it. Plan mode appends a
  short instruction to research, then call `exit_plan_mode`. Sub-agents run in their caller's mode; plan mode offers
  them, and in it a sub-agent gets its read-only tools and no `exit_plan_mode`, and answers its caller.
- **Where:** `Agents/RunOptions.cs`, `Decisions/ExitPlanModeFunction.cs`, `Approval/ModeChatClient.cs`, the gate of
  D81.
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

### D59. A call id the conversation holds already is renamed

- **Status:** approved.
- **Context:** some providers, such as local servers with an OpenAI-compatible API, number their tool calls per
  response, so a later response reuses a call id the conversation holds. `FunctionInvokingChatClient` keys approval
  requests by an id made from the call id, and the history keys calls and results by call id (D46), so a reused id made
  the run that answered an approval fail with a duplicate key, and left a call that never got a result paired with
  another call's result.
- **Decision:** a layer between the checkpoints and compaction gives a function call of the provider's response a
  numbered suffix, as `call_1_2`, when its id is in use: in the messages of the model call, in any entry the run
  loaded, its history leaving them out or not, in what the run stored since, or earlier in the same response. A result
  the provider returned with a renamed call follows it. The layer renames before the checkpoints store the response
  and before the gate and the tool loop read it, so the stored entries, the decisions' batches and the tool results
  carry the new id, and the provider receives it in later calls; the renamed call drops the provider's raw item, which an adapter
  would send back with the old id. A response with a conversation id keeps its ids, since a provider that keeps the
  conversation itself matches the results to them. The chat spans of a call that did not stream show the provider's
  ids, everything else the new ones. A decision's id is made when the run asks and never from a call id (D80), so
  an answer cannot reach another call that reuses the id.
- **Where:** `Hosting/UniqueCallIdsChatClient.cs`, `Conversations/ConversationRun.cs` (the ids the run holds),
  `AgentHost.cs`.
- **Alternatives:** dropping answered requests from the history, which leaves calls and results keyed by a reused id;
  ids Typeshift makes up for every call, which hides the provider's ids even where they are unique; translating back
  to the provider's id on the way down, which a provider that numbers per response would then see twice.

### D60. An answer to a closed request throws; steering that the run answers closes its requests

- **Status:** owner; superseded by D80, which stores whether a decision is open, and D81, which expires what waits.
- **Was:** a request was open while the conversation's last run left it without an answer, until the agent answered
  steering input stored after it. `OpenRequests` worked that out from the stored messages, and a run whose input
  answered a request that was not open threw `ApprovalClosedException`, naming each request with an
  `ApprovalClosedReason`: answered, passed by, or unknown.
- **Now:** whether a decision is open is the state of its batch in the conversation's decisions document. An answer
  to a decision that is not open throws `DecisionClosedException`, naming each with a `DecisionClosedReason`
  (`Answered`, `Expired` or `Unknown`), and records nothing (D80). New input, or steering input the run takes after
  it asked, expires the batch, and the model reads a result for each of its calls (D81).

### D80. Decisions are records in a document per conversation

- **Status:** owner.
- **Context:** the owner asked for "a proper decision system ... like a proper user Q&A tool". What a run asked your
  app was Microsoft.Extensions.AI's approval contents inside the conversation's history, and whether a request was
  open, answered or carried out was worked out by scanning messages, in six files (D24, D60). Where two of those
  scans disagreed, an approved call ran twice, and calls ran after the user had sent something else.
- **Decision:** a decision is a stored record with an id and a state: an `ApprovalDecision` for a call of a tool that
  requires approval, a `PlanDecision` for `exit_plan_mode`. A conversation has one decisions document (D79): at most
  one batch, the tools approved for the conversation, and the ids of the last batch's decisions with why they
  closed. A batch holds every call of one model response of which at least one waits for your app: with an ask for
  a call that waits, with a rejection for a call a rule rejected, bare for a call that runs with the batch. It
  repeats each call's name and arguments, so the decisions are listed and the calls replayed from the document
  alone. An answer (`DecisionAnswer`) names its decision by id. The same answer again changes nothing; another
  answer, or one to a decision that is not open, throws `DecisionClosedException` and records nothing of its call;
  one that does not fit its decision throws `ArgumentException`. Answering is an API of its own: the generated
  `AnswerAsync(answers)` records the answers and, once no decision is left open, continues the run;
  `RecordAnswersAsync` records them without a run, `ResumeAsync` continues, and `GetDecisionsAsync` lists what
  waits, with the tools approved for the conversation. No answer travels in a run's input (D81).
- **Details:** a batch is `open` until its last answer, then `answered`, then `started` once a run begins to carry
  the answers out, then gone and remembered as answered. `started` is the commit point: from it on every call the
  batch would run counts as started, so no later run starts one again. An open batch that new input passes by is
  `expired`, then gone and remembered as expired. Standing approvals join the document when their batch starts,
  never when they are recorded, so an answer that expires grants nothing. `DecisionLedger` is the only code that
  loads and saves the document. Each change is a compare-and-set on the version it loaded; after a lost save it
  loads again and checks its precondition on what the winner wrote, at most ten times. Ids are version 7 GUIDs made
  when the run asks, never from the model's call ids, which a provider may reuse (D59). `Decision` reads and writes
  its own JSON (D44), and `DecisionAnswer` binds from the JSON a page posts.
- **Where:** `Decisions/Decision.cs`, `Decisions/DecisionDocument.cs`, `Decisions/DecisionLedger.cs`,
  `Decisions/DecisionClosedException.cs`, `Hosting/AgentHost.cs` (the members that list and answer),
  `Emitting/AgentEmitter.cs` (the generated `AnswerAsync` family).
- **Alternatives:** Microsoft.Extensions.AI's contents in the history with one function that decides which are live,
  which leaves the state derived and the request ids made of call ids; `started` marked per call, a write before
  each call and a lock around concurrent ones, which the document's shape still allows later; answers all at once
  or an exception, which one button per prompt and two tabs cannot give; timeouts, which need a timer the library
  does not run, where `AskedAt` lets your app withdraw what is old.

### D81. The tool loop never pauses: a gate below it decides each call, and a replay carries the answers out

- **Status:** owner.
- **Context:** `FunctionInvokingChatClient` pauses a run itself when a tool is an `ApprovalRequiredAIFunction`. It
  turns the response's calls into approval requests, keeps them from the layers below it, and expects the answers
  in the messages of the next call. What Typeshift built around that pause had to agree with the loop's own reading
  of the history, and where it did not, an approved call ran twice or ran after the user had sent something else
  (D24, D60).
- **Decision:** the loop gets no reason to pause. The mode layer hands it every tool that requires approval in a
  wrapper that hides the mark (`GatedFunction`), and a gate between the loop and the checkpoints
  (`DecisionChatClient`) reads each model response before the loop does and gives every call that needs one a
  verdict: run, or answer with a text. Approval rules decide first: `IToolApprovalRule`s (`WithApprovalRule<T>()`,
  scoped) see the exact arguments and answer with a `RuleVerdict`, `Approve`, `Reject(reason)` (which wins over
  every approval, and throws without a reason, which would read as no opinion) or `Abstain`. Then the
  conversation's standing approvals decide, each a tool's name and, for an MCP server's tool, the server; then the
  mode (D52). A call nothing decides waits for your app: the gate opens a batch with the response's calls (D80) and
  marks every call of the response informational, which the loop does not invoke, so the run ends with the
  decisions and nothing of the response has run. Once the batch is answered, the conversation's next run marks it
  started, and the gate answers that run's first model call itself with the batch's calls, the replay. The loop
  invokes them through its function invoker (`ToolGate`), which answers a rejected call with its rejection and runs
  the others through the tool middleware (D30).
- **Details:** a call a rule, a standing approval or auto mode approves costs no extra round and no write. The
  batch is saved after the response's entry and before the loop sees the response, so a decision your app holds
  exists in the store: a save that fails, or a response whose append failed, fails the run with the store's
  exception. A run without a conversation that must ask throws `DecisionUnavailableException`, naming the tools. A
  streaming run holds back its updates from the first call of a response until the stream ends, since the loop
  copies calls as they arrive; its decisions travel in the run's last update, after the run's end is stored, so
  every decision your app sees can be answered, and that update keeps the ids, the model and the finish reason of
  the response that asked. In a response, a decision is a `DecisionRequestContent`, derived from
  `InputRequestContent`; it is never stored and never sent to a provider, and input that carries a request for
  input or an answer to one throws `ArgumentException`, naming `AnswerAsync`. New input expires a batch that still
  waits, and so does steering input the run takes after it asked: each call of the batch gets a result that says it
  did not run, the model reads them with the new input, and an answer recorded before grants nothing. A started
  batch whose run stopped is settled by the next run: each call without a stored result gets one that says it is
  not known whether the call ran, and no call starts again. A handoff, or another tool the loop cannot run, called
  next to a call that waits is answered at once as not carried out. `exit_plan_mode` asks only in a host's run in
  plan mode (D52), and a sub-agent's call that would wait is rejected (D33). The loop's invoker refuses a call of
  a tool that requires approval that reaches it without a verdict. `Decisions/ToolLoopGuardTests` pins the two
  behaviors of Microsoft.Extensions.AI this rests on: the loop invokes no informational call, and a layer below it
  may answer in the model's place.
- **Where:** `Decisions/DecisionChatClient.cs`, `Decisions/ToolGate.cs`, `Decisions/GatedFunction.cs`,
  `Decisions/DecisionRequestContent.cs`, `Approval/ModeChatClient.cs`, `Approval/ToolApproval.cs` (the rules),
  `Conversations/ConversationChatClient.cs` and `Conversations/ConversationRun.cs` (settling a batch when a run
  begins, and the replay).
- **Alternatives:** keeping the loop's pause and translating its contents at the edge, which leaves what runs to
  the loop's reading of the history; a tool loop of Typeshift's own, a second implementation of what
  Microsoft.Extensions.AI maintains; an approval callback the run awaits, which holds a run open across requests
  and loses it at a restart; showing a streaming run's decisions the moment it asks, where steering can close a
  decision the app already shows; refusing new input while decisions wait, where "no, do this instead" is how
  people answer a prompt; translating Microsoft.Extensions.AI's approval answers in a run's input, which fits a
  bridge for clients that speak that protocol, if one is built.

## Repository

### D48. Carried as they were

- Sealing (v0 D19), CS1591 off in tests only (v0 D20), samples use a scripted client (v0 D21), package metadata
  (v0 D22), releases by tag through trusted publishing (v0 D37), the Typeshift name (v0 D38), the package README
  (v0 D39), nullable parameters widened at the schema root (v0 D12).
- NuGet imports a package's build files only under the package's ID, from the folder of the project's framework, so
  the props and the targets are packed as `buildTransitive/net11.0/Typeshift.props` and `Typeshift.targets`. Outside
  a framework folder they would count as an asset for every framework, and a project on .NET 10 would restore the
  package (D61).

### D49. Diagnostics are numbered afresh

- **Decision:** the rewrite started the catalog again at TSH001, since none of v0's IDs had shipped. A number is
  taken once any tag shipped it, a prerelease included: no other diagnostic ever gets it, and a diagnostic that goes
  away stays listed under "Retired IDs" in the guide, as TSH021 does since 0.1.0-alpha.6. A new diagnostic takes the
  number after the highest ever used. Before 1.0 an ID's meaning may still change, with a CHANGELOG entry, as those of
  TSH028 to TSH030 did in 0.1.0-alpha.6. Once a stable release ships a diagnostic's row, its ID never changes (see
  [Releasing](./releasing.md#cutting-a-release)).
- **Where:** `docs/guide/diagnostics.md#retired-ids`, `Diagnostics/DiagnosticCatalogTests` (generator tests).

## Withdrawn

| ID | Was | Replaced by |
|---|---|---|
| D11 | The id type is an MSBuild property | D72 |
| D56 | A model is a provider and a model, one interface serves both | D77 |
| D24 | Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules | D80, D81 |
| D60 | An answer to a closed request throws; steering that the run answers closes its requests | D80, D81 |

## Related

- [Architecture](./architecture.md): the parts these decisions shape.
- [Status](./status.md): what is built and what is open.
