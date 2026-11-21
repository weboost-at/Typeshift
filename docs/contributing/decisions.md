---
title: Decisions
description: "Why the code is shaped the way it is: each non-obvious choice, where it lives and, for most, the alternatives that lost."
---

# Decisions

Why the code is shaped the way it is. Each entry gives the decision and where it lives, and most entries also the
alternatives that lost. The entries stand by topic, and the [index](#index) lists them by ID.

Code cites an entry as `// D25: ...`, so IDs never change. Read the entry before you change the code it names. To change
the decision, edit the entry in the same commit, or add a new one and move the old to the [withdrawn](#withdrawn) table.

**Status** says where a decision came from:

| Status | Means |
|---|---|
| owner | The owner decided it during the rewrite. |
| approved | The owner approved it with the rewrite's API sign-off. |
| brief | The rewrite's brief decided it. |
| carried (v0 Dn) | It carries v0's decision `Dn` over, as it was or with the changes named. |

An entry carried from v0 keeps its reasons in v0's log, which the history holds:
`git show cd26ef8:docs/contributing/decisions.md` prints it, with each decision's context and the alternatives
that lost.

## Index

The table lists every entry by its ID, with the section that holds it and whether it is still in force.

| ID | Decision | Section | State |
|---|---|---|---|
| D1 | [`AddTypeshift()` exists only as an intercepted call](#d1) | Registration and the generator | In force |
| D2 | [An analyzer reports the diagnostics; the generator only writes code](#d2) | Registration and the generator | In force |
| D3 | [Discovery stays attribute-free, narrowed by syntax](#d3) | Registration and the generator | In force |
| D4 | [Every declaration shape the generator cannot read is diagnosed](#d4) | Registration and the generator | In force |
| D5 | [Settings and chat-option picks are typed lambdas that never run](#d5) | Registration and the generator | In force |
| D6 | [The generated names](#d6) | Registration and the generator | In force |
| D7 | [Stamps carry the generated names and a version](#d7) | Registration and the generator | In force |
| D8 | [The generator targets Roslyn 5](#d8) | Registration and the generator | In force |
| D9 | [Every project that declares toolsets or agents references the package](#d9) | Registration and the generator | In force |
| D10 | [The hidden public surface generated code calls](#d10) | Registration and the generator | In force |
| D11 | [The id type is an MSBuild property](#d11) | Registration and the generator | Withdrawn |
| D12 | [Typed host members are generated extension blocks](#d12) | Registration and the generator | In force |
| D13 | [Tool names derive from the method](#d13) | Declarations | In force |
| D14 | [Descriptions come from XML docs](#d14) | Declarations | In force |
| D15 | [Tools receive services through `IServiceProvider`](#d15) | Declarations | In force |
| D16 | [A tool receives its settings as a parameter](#d16) | Declarations | In force |
| D17 | [One flat chain per declaration](#d17) | Declarations | In force |
| D18 | [Configurable members are an allow list unless `WithConfigurableSettings` says otherwise](#d18) | Declarations | In force |
| D19 | [Configurable types widen to what the generator can serialize](#d19) | Declarations | In force |
| D20 | [`Configure{Tool}()` stays](#d20) | Declarations | In force |
| D21 | [Instructions come from a virtual method](#d21) | Declarations | In force |
| D22 | [Agent names are optional and derive from the class](#d22) | Declarations | In force |
| D23 | [No agent inheritance, no composition block in v1](#d23) | Declarations | In force |
| D24 | [Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules](#d24) | Declarations | Withdrawn |
| D25 | [A run uses the services of the scope its host was resolved from](#d25) | Runtime | In force |
| D26 | [Runs return Microsoft.Extensions.AI's types](#d26) | Runtime | In force |
| D27 | [Four settings layers](#d27) | Runtime | In force |
| D28 | [The model is a configurable option like any other](#d28) | Runtime | In force |
| D29 | [The chat client pipeline](#d29) | Runtime | In force |
| D30 | [Middleware: one interface around runs, one around tool calls](#d30) | Runtime | In force |
| D31 | [Context providers](#d31) | Runtime | In force |
| D32 | [Run options, with compaction inside the tool loop](#d32) | Runtime | In force |
| D33 | [Sub-agents, in the foreground or the background](#d33) | Runtime | In force |
| D34 | [Handoff continues with the agent handed to](#d34) | Runtime | In force |
| D35 | [MCP servers through a host provider](#d35) | Runtime | In force |
| D36 | [OpenTelemetry is always on](#d36) | Runtime | In force |
| D37 | [No built-in toolsets](#d37) | Runtime | In force |
| D38 | [A store of documents and a store of conversations](#d38) | Storage | In force |
| D39 | [Kinds are stable names](#d39) | Storage | In force |
| D40 | [A schema version per document](#d40) | Storage | In force |
| D41 | [Optimistic concurrency](#d41) | Storage | In force |
| D42 | [Settings ids and conversation ids are separate; conversations grow by appending](#d42) | Storage | In force |
| D43 | [The store is the persistence switch](#d43) | Storage | In force |
| D44 | [Settings records serialize themselves](#d44) | Storage | In force |
| D45 | [Tool JSON: contexts from the modules, reflection where the app keeps it](#d45) | Storage | In force |
| D46 | [Tracking, carried and fixed](#d46) | Conversations | In force |
| D47 | [Live updates are a feed per conversation](#d47) | Conversations | In force |
| D48 | [Carried as they were](#d48) | Repository | In force |
| D49 | [Diagnostics are numbered afresh](#d49) | Repository | In force |
| D50 | [A conversation is stored step by step and resumes after a crash](#d50) | Conversations | In force |
| D51 | [Steering a run going on](#d51) | Conversations | In force |
| D52 | [Plan and auto modes are built in](#d52) | Conversations | In force |
| D53 | [A failed tool call tells the model what was written for it](#d53) | Runtime | In force |
| D54 | [Runs starting and ending are a feed too](#d54) | Conversations | In force |
| D55 | [The types tools take describe themselves with their XML docs](#d55) | Declarations | In force |
| D56 | [A model is a provider and a model, one interface serves both](#d56) | Runtime | Withdrawn |
| D57 | [An agent may declare tools of its own](#d57) | Declarations | In force |
| D58 | [A store's own tests check it against the contract](#d58) | Storage | In force |
| D59 | [A call id the conversation holds already is renamed](#d59) | Conversations | In force |
| D60 | [An answer to a closed request throws; steering that the run answers closes its requests](#d60) | Conversations | Withdrawn |
| D61 | [The package targets .NET 11 only](#d61) | Registration and the generator | In force |
| D62 | [Union types in tools and typed runs](#d62) | Storage | In force |
| D63 | [The default client serves only a run that names no provider](#d63) | Runtime | In force |
| D64 | [A typed run can stream](#d64) | Storage | In force |
| D65 | [A sub-agent can answer in a type](#d65) | Runtime | In force |
| D66 | [Each agent has settings of its own, under no id](#d66) | Storage | In force |
| D67 | [A store of agents' own settings in files](#d67) | Storage | In force |
| D68 | [The package turns XML docs on](#d68) | Storage | In force |
| D69 | [Without a store, everything is kept in memory](#d69) | Storage | In force |
| D70 | [Instructions that never change are one call](#d70) | Storage | In force |
| D71 | [Every conversation store implements the load from the last summary](#d71) | Storage | In force |
| D72 | [Ids are given in code, and their types pick the stores](#d72) | Storage | In force |
| D73 | [Every conversation can be kept](#d73) | Storage | In force |
| D74 | [Live updates are on by default](#d74) | Storage | In force |
| D75 | [An app without a container creates a TypeshiftRuntime](#d75) | Storage | In force |
| D76 | [The runtime is the root, and its hosts are units of work](#d76) | Storage | In force |
| D77 | [Each agent's providers are of the type its WithModel names](#d77) | Runtime | In force |
| D78 | [Stored contents follow Microsoft.Extensions.AI's content contract](#d78) | Storage | In force |
| D79 | [Decisions have a store of their own, and a setup that would lose them throws](#d79) | Storage | In force |
| D80 | [Decisions are records in a document per conversation](#d80) | Conversations | In force |
| D81 | [The tool loop never pauses: a gate below it decides each call, and a replay carries the answers out](#d81) | Conversations | In force |
| D82 | [Questions to the user are a built-in tool an agent declares](#d82) | Conversations | In force |
| D83 | [The runtime lists its agents and toolsets and hosts an agent by its kind](#d83) | Storage | In force |
| D84 | [An agent's use of a toolset may require approval for a tool and let settings switch one off](#d84) | Runtime | In force |
| D85 | [A run leaves the caller's synchronization context](#d85) | Runtime | In force |

## Registration and the generator

### D1. `AddTypeshift()` exists only as an intercepted call {#d1}

- **Status:** carried (v0 D1).
- **Decision:** the library's `AddTypeshift()` throws, saying what to check; the generator intercepts every call in a
  project. It returns `TypeshiftBuilder`, the one registration chain. A library that calls `AddTypeshift()` inside its
  own `AddX()` registers only its own modules; the docs say so. The analyzer reports the ways to reach the method
  that it can see and no interceptor rewrites, such as a method group (TSH040), so the throw is left for a project
  without the package.
- **Alternatives:** a generated method per assembly, ambiguous (CS0121) wherever `InternalsVisibleTo` reaches.
- **Where:** `DependencyInjection/`, `Emitting/InterceptorEmitter.cs`.

### D2. An analyzer reports the diagnostics; the generator only writes code {#d2}

- **Status:** approved.
- **Context:** v0 reported TSH diagnostics without syntax trees, so `#pragma` and `.editorconfig` could not reach
  them, and locations in the models broke caching.
- **Decision:** a `DiagnosticAnalyzer` in the same package reports every TSH diagnostic at real locations; the parser
  takes a report callback, a no-op in the generator. Compilation-wide checks (unique names, references, the provider id
  type) are compilation-end actions. The analyzer sees generated code, so it checks picks and the store type
  semantically. A `DiagnosticSuppressor` silences CS1573 for tool parameters the model never sees.
- **Files marked as generated are analyzed:** the analyzer asks for `GeneratedCodeAnalysisFlags.Analyze` and
  `ReportDiagnostics`. The generator reads a file that starts with `// <auto-generated/>` or is named `*.g.cs` like
  any other, so a toolset a script, a template or another generator wrote was generated or dropped without one
  diagnostic. The analyzer therefore runs over Typeshift's own output too; its checks of user members leave those
  trees out (`Names.IsGenerated`), and `AnalyzeCall` returns at once for a call that is none of the library's.
- **The two passes are compared:** the parsers are shared, the compilations are not. The generator's holds no
  generator's output, its own included, so a declaration that names such code parses in the analyzer and is dropped
  by the generator: `AddTool(Generated.Find)`, `WithStorageKind(Generated.Kind)`, a tool that takes an agent's
  `Settings`. The analyzer compares its model with what the generator wrote, the `[GeneratedTool]` stamps of a
  toolset and the `[ModuleAgent]` stamp of an agent in the compilation's own module, and reports the first
  difference as TSH032: a missing tool, another name or kind, a missing description. It is a warning, since an
  editor may hold generated code a moment behind the source, and a class without a generated part is not compared.
  A toolset's missing tool is reported on the toolset alone, not on each agent that uses it. A schema version, a
  provider type and the text of a description are in no stamp and are not compared
  (see [Status](./status.md#known-gaps)).
- **Alternatives:** the generator reporting with real locations, which keeps syntax nodes out of cached models only
  with v0's plumbing.
- **Where:** `src/Typeshift.Generators/Analysis/`.

### D3. Discovery stays attribute-free, narrowed by syntax {#d3}

- **Status:** owner.
- **Context:** v0 re-bound every class with a base list on each edit (about 170 ms per keystroke for 50 toolsets).
- **Decision:** the syntax predicate takes classes whose base list names `Toolset`, `Agent` or
  `JsonSerializerContext` (any qualification); the semantic check confirms. No marker attribute.
- **What an edit costs:** the predicate narrows which classes are bound, not when. A syntax provider runs its
  transform again for every class it passed whenever the compilation changes, so each edit, in any file, binds the
  constructors of every toolset and agent and parses them; only the outputs stay cached, while the models compare
  equal. For a toolset of three tools and an agent that uses two toolsets (`ScaleSources`) a run after an unrelated
  edit allocates about 265 KB and takes 2 to 3 ms of CPU per pair, 70% of it in binding the constructors
  (`GetOperation`); the measurements by project size are in [Status](./status.md#known-gaps). The parsers read a
  doc comment of the compilation where it is written, not from the compiler's XML, which took a third of their
  share. The search for the ids of `[Experimental]` symbols allocates only once it finds one, where it took a
  quarter of what was left.
  `IncrementalCostTests` holds the bytes under a budget per pair, and `tools/Typeshift.Tools.Generators` measures
  the time before a release. Reading each chain from syntax and binding only what it names would cut the binding,
  and changes the parsers the analyzer shares (D2) and the shapes D4 reports; it waits for projects that need it.
- **A using alias is not followed:** the predicate reads the name in the base list, so a class that names its base
  through an alias, `using AgentBase = Typeshift.Agent;` in an app with an `Agent` of its own, is never generated.
  The analyzer, which starts from the symbol, reports it as TSH017 on the alias and parses no further; before, the
  class compiled, the agent offered nothing and failed at run time. A context named through an alias is not
  registered and lists nothing for TSH027.
- **Alternatives:** a marker attribute with `ForAttributeWithMetadataName`, which adds ceremony to every declaration
  and saves nothing: it too runs its transform again for every marked class after an edit of another file. Following
  aliases in the predicate: one in the same file is cheap to resolve, but a `global using` alias lives in another
  file and needs the compilation, which the predicate exists to avoid.
- **Where:** `Parsing/Discovery.cs`.

### D4. Every declaration shape the generator cannot read is diagnosed {#d4}

- **Status:** approved.
- **Context:** v0 ran the declaration chains at runtime too, and read the tool picks from syntax at compile time, so a
  chain split over variables offered one set of tools at runtime and generated code for another.
- **Decision:** `Include(t => t.X)` stays, picked from the generated `ToolList<T>` (v0 D29): a foreign tool does not
  compile. The generator reads the picks from syntax, since the list's properties are its own output; the analyzer
  reports every other shape: a chain split over variables, a pick that is not `t => t.X` (TSH015), a declaration
  outside the constructor body (TSH014). At runtime the chains declare nothing: the generated code is the one truth,
  and only values (`WithDisplayName`, chat options, `Configure{Tool}()`) are recorded.
- **Constructors that never run:** the module creates each toolset and agent with its parameterless constructor,
  which runs only the constructors it chains to with `this(...)`. Up to 0.1.0-alpha.6 the generator read the
  declarations of every constructor, so a pick or a toolset use in another one reached the generated `Settings` and
  tools while its values, such as the instructions, never reached a run; only the provider id type followed the
  chain (D77). The parsers read that chain's blocks alone (`ConstructorChains.GetRun`), and the analyzer reports a
  declaration in any other constructor (TSH044), a toolset's too, whose instance records its `WithDisplayName`. A
  class without a parameterless constructor is TSH016 already and keeps the blocks of all its constructors.
- **Alternatives:** method groups (`Include(TicketToolset.SearchTickets)`), which bind semantically but take a
  `Delegate` and fail only through a diagnostic.
- **Where:** `Parsing/AgentParser.cs`, `Parsing/ConstructorChains.cs`, `Analysis/`.

### D5. Settings and chat-option picks are typed lambdas that never run {#d5}

- **Status:** carried (v0 D5), typed.
- **Decision:** `Configurable<TMember>(Func<TSettings, TMember>)`: no `object`. The generator reads the picks; the
  configurable set exists once, in the generated records.
- **Where:** `Toolset.cs`, `Agent.cs`, `Parsing/ConstructorChains.cs`.

### D6. The generated names {#d6}

- **Status:** carried (v0 D2), extended.
- **Decision:** per toolset a `{Tool}Overrides` record per tool with configurable settings, a nested `ToolNames`
  class of one constant per tool (D13), which an agent with tools of its own gets too, a hidden nested class
  `TypeshiftGenerated` of each tool's factory and function, and a
  `Tstools_{full name}` extension block in the global namespace (D12); per agent the nested `Settings`
  record, a private `Configure{Tool}()` per offered tool with settings and a `Tshost_{full name}` extension
  block in the global namespace (D12); per assembly a module. Keywords get `@`. A clash with a user member is
  TSH018, and so is
  a class named like a member written into it. Two tools of one class that would share a generated member are
  TSH007. A generated file is named after its class's full name. The compiler compares those file names without
  case, so where two full names differ only by case, each file's name also carries a hash of its class's spelling.
- **A tool's factory and function:** `Create{Tool}` and the private `{Tool}Function` were members of the toolset up
  to 0.1.0-alpha.6, so a method `CreateReport` beside `AddTool(Report)` was TSH018, and so was a toolset named
  `CreateTicket` with a tool `ticket`. They live in `TypeshiftGenerated` now, a public static class, hidden, since an
  agent of another assembly calls the factory, and the stamp names it from the toolset,
  `TypeshiftGenerated.Create{Tool}` (D7). Two tools whose factory and function share a name there stay TSH007. An
  agent's own tools have theirs in a class `Tools` inside the agent's `TypeshiftGenerated`, apart from the builders
  of `Configure{Tool}()`: `x_builder`'s factory would take the name of `create_x`'s builder. `{Tool}Overrides` and
  `ToolNames` stay members of the class, since an app names them.
- **Names a generated type already has:** a generated property never takes the name of a member its type has from
  `object` (`Names.ObjectMembers`) or declares itself, and gets a suffix instead: `Tool` on the tool list and in an
  agent's `Settings`, `Value` in an overrides record. A tool's constant in `ToolNames` takes the name of its property
  on the tool list, so `tool_names` is `ToolNamesTool` in both. Each type keeps a list of its own members beside the shared
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
  declaration, the builders, the per-agent store of `Configure{Tool}()` values and the factories and functions of
  the agent's own tools) and one private field
  `_typeshift`, so an agent gains as few names as possible. A tool's member in `Settings` named like an option or a
  record member gets a `Tool` suffix, and one more while another offered tool has that name: beside `kind_tool`,
  the tool `kind` is `KindToolTool`. A public agent's `Settings` cannot name the overrides of an internal toolset:
  TSH006, and the generator leaves the member out. Nor can it name a less accessible provider id type, the agent's
  own (D77), which a configurable model or summary model brings in: TSH006 on the pick, and the generator writes the
  provider members internal.
- **Where:** `Naming/Names.cs`, `Emitting/AgentEmitter.cs`, `Emitting/ToolsetEmitter.cs`,
  `Emitting/ModuleEmitter.cs`, `Emitting/StorageEmitter.cs`.

### D7. Stamps carry the generated names and a version {#d7}

- **Status:** approved.
- **Context:** v0's stamps carried tool names only, so an agent rebuilt `{Tool}Overrides` names with its own
  generator version.
- **Decision:** tool stamps name the overrides type, the list property and the factory, from the toolset, as
  `TypeshiftGenerated.Create{Tool}` since stamp version 20 (D6), and every module stamp has a stamp version; an
  unknown version is TSH020.
- **What the version covers:** everything generated code binds to, since code generated under one version runs
  against the library of another once a referenced project was built earlier. That is the shape of the stamps, every
  member of `Typeshift.Generated`, `IStorable<TSelf>` and `Agent.Declaration`. A change to any of them raises
  `KnownTypes.StampVersion`, so the reference built before it gets TSH020 at build time, where it would fail
  with a `MissingMethodException` when the app starts. `PublicApiSurfaceTests` keeps those members in
  `Snapshots/GeneratedContract.verified.txt` under the version, and fails when they change while it stays.
- **Where:** `Generated/Stamps.cs`, `Naming/KnownTypes.cs`, `Emitting/ModuleEmitter.cs`, `Parsing/ReferenceReader.cs`,
  `Repository/PublicApiSurfaceTests` (generator tests).

### D8. The generator targets Roslyn 5 {#d8}

- **Status:** approved.
- **Decision:** consumers need C# 14 for the extension blocks anyway, so the generator uses Roslyn 5 APIs, and v0's
  4.14 pin and its trap are gone.

### D61. The package targets .NET 11 only {#d61}

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

### D9. Every project that declares toolsets or agents references the package {#d9}

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

### D10. The hidden public surface generated code calls {#d10}

- **Status:** carried (v0 D35).
- **Decision:** what only generated code calls is public and `[EditorBrowsable(Never)]`, in `Typeshift.Generated`.
- **Marked as generated:** every generated type, and every member generated into a class of the user's, carries
  `[GeneratedCode]`, with the generator's version, and `[ExcludeFromCodeCoverage]` (`Names.Generated`). The user's
  partial class never does: analyzers skip what the attribute marks, the Typeshift analyzer among them, and would
  skip the user's own code in it. The host's members and the tools' factories also carry `[DebuggerStepThrough]`.
  The version is the package's, without the commit a build appends, read once per process, so it keeps the
  generator's outputs cached.

### D11. The id type is an MSBuild property {#d11}

- **Status:** owner; superseded by D72. `<TypeshiftIdType>` typed every settings and conversation id of the build;
  now each `WithSettingsId` and `WithConversationId` types the ids of its own call.

### D12. Typed host members are generated extension blocks {#d12}

- **Status:** carried (v0 D4).
- **Decision:** `extension(AgentHost<TAgent> host)` per agent, in the agent's assembly, with `RunAsync`, `RunAsync<T>`,
  `RunStreamingAsync`, `ResumeAsync`, `ResumeStreamingAsync`, `AnswerAsync`, `AnswerStreamingAsync` (D80, at stamp
  version 8), `LoadSettingsAsync`, `SaveSettingsAsync`, `UpdateSettingsAsync`, `ResolveOptionsAsync` and
  `CreateChatClientAsync` (D26, at stamp version 15). They call hidden library methods, so the logic lives and is
  tested in the library.
- **The settings members:** `UpdateSettingsAsync` was `ConfigureAsync` up to 0.1.0-alpha.6, and returned the settings
  without the version its save wrote, so a settings form that saved again had to load first. The name read as
  configuring the host or its next run, beside `Configure{Tool}()` for code defaults and `Configurable(...)` for what
  may be stored. It now carries the noun its siblings carry, returns the settings with their version as
  `LoadSettingsAsync` does, and throws when its function returns null, which the save failed on later without naming
  the function. A generated member has no cheap forwarder, so the old name went without an `[Obsolete]` twin.
- **The host's own runs:** every run, answer and resume, `ResolveOptionsAsync` and `CreateChatClientAsync` are also
  instance members of `AgentHost<TAgent>` without the `Settings` parameter,
  `RunAsync(string input, CancellationToken ct = default)` for example. Code generic over the agent, a helper for every agent's endpoint or test, calls those: an extension block
  on the closed `AgentHost<SupportAgent>` is not in reach of an `AgentHost<TAgent>`, which was CS1929. An instance
  member wins where it applies, and a call with settings, by position or by name, reaches the generated one, so
  `RunAsync(input, ct)` takes the token second, where it was CS1503 and every call wrote `ct: ct`.
- **Namespace:** the blocks live in the global namespace, in a file of their own per agent, so every file has them in
  scope. In the agent's namespace, a file that named the agent in full or through an alias saw no typed members, and
  the owner asked why `RunAsync` was missing at times; the `Typeshift` namespace would have hidden them from a file
  that names `AgentHost` in full. Each class spells out the agent's full name, every part after its length, so two
  agents never share one, whatever their namespaces hold.
- **Tool lists:** the extension block on `ToolList<TToolset>`, which `Include(t => t.X)` picks from (D4), follows the
  same rule: the global namespace, a file of its own per toolset, a hidden class `Tstools_{full name}` with
  every part after its length. It lived in the toolset's namespace as `{Toolset}ToolList` first. There a pick was
  CS1061 in a file that named the toolset in full or through an alias, as the host members had been, and the class
  shared its name with any type of the namespace called `{Toolset}ToolList`, and with the list of a nested toolset
  of that flat name.
- **The prefixes:** `[EditorBrowsable(Never)]` hides a class from code completion only where it comes from a compiled
  reference, so in the project that declares an agent, where most code is written, every class of the global
  namespace is offered. The classes were `TypeshiftHost_` and `TypeshiftTools_`, and the build's schema annotations
  class (D55) `TypeshiftSchemaAnnotations_`, so `Type` offered one of each agent and toolset beside
  `TypeshiftRuntime` and `TypeshiftBuilder`. They are `Tshost_`, `Tstools_` and `Tsannotations_` now, one word
  each: a probe of Roslyn's completion matched none of them for `Type`, `Host` or `Tool`, and each still for the
  full name it spells out, such as `Support` for `Tshost_4Demo12SupportAgent`. Only a hash in place of the name
  would avoid that, and it would cost a readable name in a stack trace and the guarantee that two never meet. The
  annotations class stays in the global namespace, where the `Annotated` members Weboost.SchemaAnnotations writes
  beside it are in scope for your code; in `Typeshift.Generated` they would need a `using`. The owner left the rename,
  free for users since no code of theirs names the classes, to the change that makes it.

## Declarations

### D13. Tool names derive from the method {#d13}

- **Status:** owner.
- **Decision:** `SearchTickets` is `search_tickets`; an `Async` suffix is dropped, so `CloseTicketAsync` is
  `close_ticket`. `WithName` overrides. The name keys stored settings, so renaming a method renames the tool: the docs
  say to pin the old name with `WithName` first. TSH007 also compares the generated property names. An agent's own
  tools are compared only across the handoffs of a conversation (D57).
- **Names outside a chain:** an approval rule, a tool middleware or a UI matches calls by name. The generator writes
  each tool's name as a constant, `TicketToolset.ToolNames.CloseTicket`, which a `switch` arm and a property pattern
  take, so renaming a method breaks the build where a rule names the old tool. A string literal kept compiling and
  matched nothing, and a rule that guarded auto mode failed open. The tools a run offers itself have public
  constants in `BuiltInTools`, which `ReservedToolNameTests` holds to the generator's own.
- **Alternatives:** keeping `Async` (`close_ticket_async`), which leaks a C# convention to the model; a stored key
  separate from the name, a second identity; the tool list's `ToolName` properties outside a chain, which exist only
  on a `ToolList<T>` no code can create, and are no constants a pattern takes.
- **Where:** `Parsing/ToolsetParser.cs`, `Emitting/ToolsetEmitter.cs`, `Toolsets/BuiltInTools.cs`.

### D14. Descriptions come from XML docs {#d14}

- **Status:** carried (v0), `WithDescription` overrides; parameters the model never sees need no `<param>` (D2's
  suppressor). An agent's summary describes it as a method's describes its tool, at the owner's request.
- **`[Description]`:** Microsoft.Extensions.AI reads `System.ComponentModel.DescriptionAttribute`, and every sample of
  it writes one. A tool brought over kept its attributes, got TSH009 for its method and nothing for a parameter,
  whose schema then had no description, though the same attribute on a record's property reached it. The parser now
  reads the attribute of the method and of each parameter where `WithDescription`, the `<summary>` or the `<param>`
  says nothing, so the XML docs stay the source that wins. The owner left a choice in line with the library's aim,
  tools described without a second schema to keep in step, to the change that makes it.
- **Alternatives:** a warning that the attribute is ignored, which keeps one source and still loses the parameter's
  description; a line in the guide alone.

### D55. The types tools take describe themselves with their XML docs {#d55}

- **Status:** owner.
- **Context:** a `<param>` doc described a parameter, but nothing described the properties of the record it took,
  so a command's ids and codes reached the model without the docs that say how to fill them.
- **Decision:** the package depends on Weboost.SchemaAnnotations and passes its generator to every consumer. The
  package's build adds a hidden `[SchemaAnnotations]` class to each project with `GenerateDocumentationFile`, named
  after the assembly as `Tsannotations_{Assembly}` (D12), and the module registers the docs collected for every
  marked class of its assembly. Tool
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
  `Generated/ToolSchemas.cs`.
- **Alternatives:** a chain call taking `AIJsonSchemaCreateOptions`, which every host would have to make; the
  generator reading the docs itself, a second copy of Weboost.SchemaAnnotations; finding a class the project marked,
  one more line of setup per project.

### D15. Tools receive services through `IServiceProvider` {#d15}

- **Status:** owner.
- **Context:** the brief asked to investigate injecting resolved services as parameters.
- **Decision:** tools stay static and take `IServiceProvider`, the run's services. No injection. A parameter that
  reads like an injected service, an interface, an abstract class or a delegate that JSON cannot create, is a
  warning (TSH038): the model would have to fill it, and every call would fail. Collection interfaces,
  System.Text.Json's nodes and types that name a converter or their derived types are JSON's and pass.
- **Alternatives:** `[FromServices]` parameters (one attribute); inference by type (interfaces are services), leaky
  for collection interfaces and concrete services such as a `DbContext`.

### D16. A tool receives its settings as a parameter {#d16}

- **Status:** carried (v0 D40). The settings, `IServiceProvider`, `CancellationToken` and `AIFunctionArguments` are
  bound by type, nullable or not, and left out of the schema; a parameter JSON cannot fill, such as `ref`, `out` or
  `dynamic`, is TSH004. A record parameter named `settings`, or of a type another tool of the class names as its
  settings, on a tool whose chain names none is a warning (TSH038): the model would see and set every member.

### D17. One flat chain per declaration {#d17}

- **Status:** carried (v0 D41), brief. No builder inside a lambda anywhere: toolsets, agents,
  registration. Metadata goes through `With...` calls, capabilities through `Use...` calls. A tool's chain takes its
  calls in any order: the generator reads them by name, and the builders do nothing at run time, so the settings
  builders offer `WithName`, `WithDescription`, `RequiresApproval` and `ReadOnly` too, and `RequiresApproval()` after
  `WithSettings<T>()` is no compiler error that reads as a rule.

### D18. Configurable members are an allow list unless `WithConfigurableSettings` says otherwise {#d18}

- **Status:** carried (v0 D43).

### D19. Configurable types widen to what the generator can serialize {#d19}

- **Status:** owner.
- **Decision:** besides v0's scalars, enums and string lists: `DateOnly`, `TimeOnly`, `Uri`, records, record structs
  and classes of storable settable members (replaced as a whole, never merged), arrays, lists and sets of storable
  values, dictionaries keyed by `string` or an enum. The generator writes their serialization member by member, as
  for the records themselves; anything else is TSH012. Recursive types are TSH012.
- **Details:** a stored record or class needs a public parameterless constructor; its members without a setter are
  computed and not stored, its obsolete ones no longer stored. A missing member reads as the constructor's value.
  Collection items and dictionary values are values: a nullable item has no stored form, `int?` and `string?`
  alike (a `List<string?>` member gave CS8619 and CS8620 inside generated code). A class may still be null where its
  type says it is not, so the generated writers check each item of a reference type and throw an
  `InvalidOperationException` that names the settings member; such a null failed the save with a
  `NullReferenceException`, or was written as a JSON null that reads back as no item. A record struct
  stores like a record. Other structs than the scalars are not storable, since most keep their state where no setter
  reaches and would store as an empty object. A class without a stored member is refused for that reason too:
  `object`, a marker class, a class whose only members have no setter, each also as an item or a dictionary value.
  Such a member compiled once, was stored as `{}` and read back as an empty object.
- **Framework classes:** a referenced class or record struct whose outermost namespace is `System` or `Microsoft`
  is refused unless the list above names it, since none is a settings class: it would store what its setters happen
  to reach, `StringBuilder` its capacity and length, as it did up to 0.1.0-alpha.6. A type of the project's own
  source passes under any namespace, so an app whose namespaces begin with `Microsoft` keeps its records. The
  namespace is read from the symbol, with
  no lookup, and reads the same on every machine; telling a framework assembly by its name would take a list that
  grows with every package, and by its public key one more read of metadata per type. A class of another
  library that has settable members still passes and stores what its setters reach: no rule tells it from a
  settings class of a library.
- **One format each:** a `TimeSpan`, a `DateOnly` and a `TimeOnly` read only in the format their writers write, the
  constant format and ISO 8601. The lenient parsers read `"30"` as thirty days and `"01/02/2026"` as the second of
  January, in a file a person edits by hand. `TimeSpan.TryParseExact` with the constant format still takes `"30"`
  and `"1:30"`, so a time span reads only when it is written back as the same text. Any other form counts as missing,
  as a member of the wrong type does (D44).
- **Where:** `Parsing/Storage.cs`, `Emitting/StorageEmitter.cs`, `Generated/StorableJson.cs`.

### D20. `Configure{Tool}()` stays {#d20}

- **Status:** carried (v0 D30), brief.
- **Decision:** a generated private method per offered tool with settings, returning a builder with a `With...` per
  settable member. Nothing shorter fits the one-chain rule, since a picker (`Configure(t => t.X)`) still needs a
  generated builder per tool and adds a step. A `Configure{Tool}()` the agent declares itself replaces the
  generated one.

### D21. Instructions come from a virtual method {#d21}

- **Status:** carried (v0 D42), changed.
- **Decision:** `GetInstructions(IServiceProvider services)` returns the agent's instructions, and Typeshift calls it
  each time it resolves a run's options, with the services of the host's scope, as tools receive them (D15). An
  agent is one instance for every run, so what the instructions depend on, such as the customer a run serves, comes
  from the services. Up to 0.1.0-alpha.6 it took nothing, so an override could only return a constant, and every
  override in the samples and demos did; those moved to `WithInstructions` (D70).
- **Alternatives:** keeping the parameterless method beside the new one, two overrides for one job where the one an
  agent overrides decides which runs; a context of the run's ids and input, whose input a resume and the raw path
  lack and steering changes within a run, while a context provider sees each pass's messages.

### D22. Agent names are optional and derive from the class {#d22}

- **Status:** carried (v0 D31), changed.
- **Decision:** without `WithName`, an agent goes by its class name in snake_case, like a tool, so it can be a tool or
  a handoff target. Names need not be unique; nothing is stored by name (D39).

### D23. No agent inheritance, no composition block in v1 {#d23}

- **Status:** owner.
- **Decision:** agents and toolsets derive from `Agent` and `Toolset` directly (TSH017). Shared declarations come
  from static methods and constants. A preset class folded into agents (`UsePreset<T>()`) was considered and
  deferred.

### D57. An agent may declare tools of its own {#d57}

- **Status:** owner.
- **Context:** a tool only one agent needed still took a toolset class of its own and a `UseToolset<T>()`.
- **Decision:** `AddTool(method)` in an agent's constructor, with the toolsets' chain, backed by a static method of the
  agent. The agent offers its own tools after its toolsets' tools. They do not form a toolset: they get no tool list, no
  toolset stamp and no registration, so no other agent offers them and `Include` and `Exclude` do not pick them.
- **As built:** the generator reads them with the toolsets' parser and writes their overrides into the agent and their
  factories and functions into its `TypeshiftGenerated` class (D6), where the agent's `Settings`, its
  `Configure{Tool}()` methods and its declaration list them as they
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

### D24. Tool approval uses Microsoft.Extensions.AI's contents, with standing approvals and rules {#d24}

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

### D25. A run uses the services of the scope its host was resolved from {#d25}

- **Status:** owner.
- **Context:** v0 gave each tool call a fresh scope of the root container, so request-scoped services (the current
  customer) never reached tools, context or the chat client provider (audit finding 2).
- **Decision:** `AgentHost<TAgent>` is transient and keeps the provider it was resolved from, or the scope
  `TypeshiftRuntime.CreateHost<TAgent>()` created for it (D76). Tools, context providers, middleware, the MCP tool
  provider and the chat client provider resolve from it. Agents and toolsets stay singletons, the store too.
- **Never the root's:** a host the container resolves from the root provider, as one injected into a singleton or
  a `BackgroundService` is, throws an `InvalidOperationException` that names the runtime and a scope. The guide
  only warned up to 0.1.0-alpha.6: with scope validation such a host failed at its first scoped service with the
  container's message, and without it, as in ASP.NET Core outside Development, every customer's run got one
  customer's scoped services, the key of its chat client provider among them, and the root kept each host until
  the app stopped. The transient registration's factory tells the root by reference: the root hands it the
  provider the runtime holds (D76), a scope another. A container that hands the root's factories some other
  object is not recognized, and resolves as before. The hosts the library builds itself, for sub-agents and
  handoffs, take the run's services and pass no such check.
- **The run's ids:** `AgentRun.TryGetSettingsId<TId>` and `TryGetConversationId<TId>` read the ids the host was
  given, typed at the call as D72 types them, so middleware, context providers, rules and, through `call.Run`, tool
  middleware need no scoped holder of the app's own for them.
- **Alternatives:** a run-context tool parameter carrying the typed id, turned down while built-in toolsets compiled
  before the host could not type it, a reason D37 removed; the typed reads on `AgentRun` serve the same need;
  per-call scopes, which lose the request; a logged warning for a host from the root, which leaves one customer's
  services with all the others in production.
- **Where:** `AgentHost.cs`.

### D26. Runs return Microsoft.Extensions.AI's types {#d26}

- **Status:** owner.
- **Decision:** `RunAsync` returns `ChatResponse`, `RunAsync<T>` `ChatResponse<T>`, `RunStreamingAsync`
  `ChatResponseUpdate`s; messages carry the answering agent as `AuthorName`. Inputs are a `string` or a
  `ChatMessage` (for images). What a run asks your app comes back in the response as `DecisionRequestContent`s,
  listed by `response.Decisions`, and is answered through `AnswerAsync`, never in an input (D80, D81). The raw path (`ResolveOptionsAsync`, `CreateChatClientAsync`, the client)
  stays; options it resolved remember their settings id, so the conversation id is the only string.
- **The raw path's client keeps its options:** the guide offers the raw path to hand the client to a library that
  takes an `IChatClient`, and such a library calls it without options or with `ChatOptions` of its own. Such a call
  ran the bare model, the provider's default model without the agent's instructions or tools, and options resolved
  for another provider sent their model to the provider the client was created for. So `CreateChatClientAsync`
  takes only options `ResolveOptionsAsync` returned and throws an `ArgumentException` for others, and the client
  keeps them. A call without options runs with them. A call with options of its own lays over them its conversation
  id, its response format and the tools it adds; the agent's instructions, model, provider, run options and tools
  stay, as the agent's settings decide them. Agent Framework passes such options per run, merged from its own.
  Options resolved for the client's provider and model run whole, their run options too, since the tool loop is
  built per call; options resolved for another throw an `InvalidOperationException`, until a client is resolved at
  the model call. `CreateChatClientAsync(settings)` resolves the options and creates the client in one call, so the
  documented use, a client handed to a library, is one line; `CreateChatClientAsync(options)` stays for code that
  reads the options too.
- **Where:** `Hosting/AgentHost.cs` (`CreateChatClientAsync`), `Hosting/ResolvedChatOptions.cs` (`ForCall`),
  `Conversations/ConversationChatClient.cs`.
- **Alternatives:** an own `AgentResponse`, a type to learn for what `ChatResponse` holds. For the raw path: a
  call's own options in place of the client's, as before, which dropped the agent; every value of a call's own
  options over the client's, which lets a library's model or temperature pass the agent's settings by; a client
  resolved for each call's provider, which is the larger change of resolving the client at the model call.

### D27. Four settings layers {#d27}

- **Status:** brief. Code defaults, the agent's code, its stored `Settings`, the run. Stored tool
  defaults (`ToolDefaults`) are gone, so resolving a run's settings is one document. The stored layer holds the
  agent's own settings and, over them, those under the settings id (D66).

### D28. The model is a configurable option like any other {#d28}

- **Status:** owner; changes v0 D34.
- **Decision:** `Configurable(o => o.ModelId)` puts `Model`, and its `Provider` (D77), into the agent's `Settings`,
  overriding `WithModel`; without it the model stays in code. The allow list holds for the model too. The options
  that may be configurable are the model, the instructions, the sampling options, the stop sequences, multiple tool
  calls and reasoning; the rest belong to the run or have no stored form (TSH012).
- **A name at all (owner: traps are loud):** a blank model or provider is refused where it enters, as a run option
  out of range is (D32): `WithModel` and `WithSummaryModel` throw, a save refuses blank settings and a run that
  resolves one throws by the member's name. Before, a settings form's empty field was stored and sent to the
  provider as the model. Whether a model exists stays the provider's to say.
- **Alternatives:** `Model` in every `Settings` (v0, the brief's first sketch), which lets a settings form a
  customer edits pick any model the provider knows.

### D84. An agent's use of a toolset may require approval for a tool and let settings switch one off {#d84}

- **Status:** from the audit of November 2026; the owner's line for the phase took the recommended option, both
  steps. In force until the owner decides otherwise.
- **Context:** which tools an agent offered was fixed in code. No setting switched a tool off for one customer,
  "web search on for this customer, off for that one", and an agent could not put a toolset's tool behind approval,
  though `UseMcpServer(...).RequiresApproval()` could for a server's. A run middleware that edits
  `run.Options.Tools` by name did both, unseen by the analyzer and the stamps.
- **Decision:** `UseToolset<T>()`, and the `Include` and `Exclude` uses after it, take
  `RequiresApproval(t => t.X)` and `Configurable(t => t.X)`, picks read from syntax as `Include` is (D4, D5).
  `RequiresApproval` makes the agent wrap the tool in `ApprovalRequiredAIFunction` when its toolset does not, so
  its calls are decisions like any (D80, D81), in this agent only. `Configurable` follows D18's allow list and D28's
  configurable options for a whole tool: the agent's `Settings` gain `bool? {Tool}Enabled`, stored as a member of
  the JSON beside the options, and a layer that sets it to false leaves the tool out of the run's tools, which
  `CreateTools` builds. Null keeps the layer below, and with every layer null the tool is offered.
- **As built:** `Catalog.Resolve` gives a tool the use marks `RequiresApproval`, so the module stamps it among the
  agent's approval tools and TSH024 and TSH037 see it, also from a referencing project; the stamps' shape is
  unchanged. A mark on a tool the use does not offer, because it excludes it or includes others only, is TSH043. A
  switch's member yields to every other member of the record, as a tool's overrides do (D6), with a `Tool` suffix.
  The runtime's catalog (D83) lists an agent's switchable tools in `AgentInfo.SwitchableTools`, which the generated
  declaration overrides, so a settings page needs no list of its own; a toolset's `ToolInfo` has none, since a use
  makes a tool switchable, not its toolset.
- **Alternatives:** step 1 alone, the default without the owner, which leaves the per-customer tool set to a
  middleware by name; `ReadOnly(t => t.X)` on a use, which would let an agent offer a toolset's tool in plan mode
  that its author did not mark, so an agent may make a tool stricter, never looser; a switch on every tool, which
  would put every tool of every agent into its `Settings` and a settings form.
- **Where:** `Agents/ToolsetUse.cs`, `Parsing/AgentParser.cs` (`ReadUse`), `Parsing/Catalog.cs`,
  `Emitting/AgentEmitter.cs` (`SwitchMember`, `WriteCreateTools`), `Models/AgentModel.cs` (`ToolsetUseModel`).

### D56. A model is a provider and a model, one interface serves both {#d56}

- **Status:** owner; superseded by D77. The MSBuild property `TypeshiftProviderIdType` typed the providers of the
  whole build; now each agent's providers are of the type its `WithModel` names.

### D77. Each agent's providers are of the type its WithModel names {#d77}

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
  keyed clients serve any type, since keyed services take keys of any type. `WithChatClientProvider<T>()` registers `T`
  as each `IChatClientProvider<X>` it implements, of any `X`, as `WithStore<T>()` registers a store, and a `T` that
  implements none is TSH030. Settings that store a provider need a type they store as one value, a string, a number,
  a `Guid` or an enum: TSH029, on the provider. An agent whose settings store none may name a provider of any type.
  Settings that store one need a type the generated `Settings`, in a file of their own, can name, so no type nested
  as `private` or `protected` and no `file` type, and a public agent's settings a public one (TSH006); the
  generator writes `string` in place of a type it cannot name, so its code compiles. The `WithModel` that
  `new TAgent()` runs last types the providers, as the module creates an agent with its parameterless constructor:
  the last `WithModel` of that constructor, or else of the constructors it chains to with `this(...)`, nearest
  first. `WithModel(model)` (owner) names a model and no provider, so an agent can mean the client registered for
  every agent (D63): it sets `ModelId` alone, keeps a provider an earlier `WithModel` named, and the parser passes
  it by, so an agent with no other `WithModel` has `string` providers. A stored provider that is no value of the
  type, such as a member an enum no longer has, throws when the settings load, naming the stored text
  (`StorableJson.EnsureProvider`, which raised the stamp version): read as absent, like any other member that does not read
  (D44), it left the model stored beside it to the provider of the layer below. Gone with the build's type: `TypeshiftProviderIdType`, the
  `IChatClientProvider` alias, the type parameter of `AddCoreServices`, `ChatClients<TProviderId>`, the module
  stamps' provider id type (stamp version 6), TSH021, and the build-wide parts of TSH029 and TSH030.
- **Where:** `Hosting/ProviderId.cs`, `Hosting/ChatClients.cs`, `Hosting/IChatClientProvider.cs`,
  `Agents/ChatOptionsBuilder.cs`, `Generated/AgentDeclaration.cs`, `DependencyInjection/TypeshiftBuilder.cs`,
  `Compaction/Compaction.cs`, `Parsing/AgentParser.cs`, `Emitting/AgentEmitter.cs`, `Naming/OptionMembers.cs`,
  `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** a model record with a request type, a client cache and a catalog, more surface than the problem
  needs; methods that list providers and their models, which Typeshift never calls; a summary provider in code, tied
  to a provider the user may switch away from; compaction that drops turns without a summary model. For the type:
  the MSBuild property, one type per build (D56); one type per app on the `AddTypeshift()` chain, known only in the
  project that registers; a type each agent declares beside `WithModel`, which says it twice; looking the provider
  up by its value's runtime type, which needs `MakeGenericType` and misses a provider whose runtime type differs from
  its declared one.

### D63. The default client serves only a run that names no provider {#d63}

- **Status:** owner; rewritten. It read "a registered chat client serves a run no `IChatClientProvider` serves",
  and let the client registered for every agent answer a run whose provider nothing served.
- **Context:** the owner asked that an agent without a model still run, and for a simpler setup than writing an
  `IChatClientProvider`: one provider, or a client per provider, for a demo or a local test. The first rule took the
  first of three that served, the last of them the client for every agent. So `WithModel("opnai", ...)`, a stored
  provider in another case, a member an enum no longer has and an enum agent beside a provider class for strings all
  ran on that client, which may be another account, without a log line. The owner's rule: "A user may configure a
  default provider and model. If not configured it should throw. If the user tries to resolve a provider which does
  not exist, throw. If they want to use the default provider on purpose, use the default provider."
- **Decision:** the provider a run names decides alone which client serves it.
  - A run that names no provider takes the default client: the `IChatClient` registered for every agent, as
    `AddChatClient` and `TypeshiftRuntime.Create(client)` register it (D75). Without one it throws. Its model, as
    `WithModel(model)` names it (D77), reaches the client as `ModelId`; without one the client's own model answers.
  - A run that names a provider takes the client of the `IChatClientProvider` of the agent's provider type (D77)
    when one is registered. The class decides alone for the providers of its type: when it throws, no client is
    tried after it, and a run that names a provider and no model throws, since the class serves a provider and a
    model. Before, such a run went on to a client keyed by the provider, which beside a class that serves a key per
    customer is the app's own key.
  - Without a class of the type, the run takes the `IChatClient` registered under the provider as its key, as
    Microsoft.Extensions.AI's `AddKeyedChatClient` registers one. It receives the model as `ModelId`.
  - A provider nothing serves throws. The message lists the keys clients are registered under, which tells a typo
    from a provider the app never registered. The default client is never asked.
- **As built:** the run's client is found before the run stores an entry or opens its span
  (`AgentHost.CreateChatClientAsync`). With a conversation, the run has claimed and loaded it by then (D34), and
  frees the claim when no client is found. A summary's provider is checked there too once the run can compact, without
  asking a provider class, which is asked when a summary is needed: a summary call that fails is best effort (D32),
  a provider that does not exist is not. The keys are read from the registrations (`RegisteredChatClients`), since
  a container does not list them.
- **Where:** `Hosting/ChatClients.cs`, `Hosting/ProviderId.cs`, `Hosting/AgentHost.cs`, `Compaction/Compaction.cs`,
  `Generated/ModuleRegistration.cs`.
- **Alternatives:** the fallback with a warning log, which still sends the prompt to the other account; the
  fallback only where no keyed client and no provider class is registered, which keeps the trap for the app with
  one client and a typo; chain calls of Typeshift's own, such as `WithChatClient(client)`, which repeat what
  Microsoft.Extensions.AI's registration already does; a built-in provider keyed by a registry of Typeshift's, a
  second place to register clients.

### D29. The chat client pipeline {#d29}

- **Status:** approved.
- **Decision:** outermost first: conversation tracking (resume, run boundaries, settling the decisions that wait,
  live updates, the `invoke_agent` span), run middleware, context providers, the mode (the tools of the run's mode
  and each pass's share of the limit), `FunctionInvokingChatClient` (handoff, run options, the tool gate and tool
  middleware as its `FunctionInvoker`), the decision gate (D81), checkpoints (each step appended before compaction
  sees it; steering input and background answers injected here), unique call ids (D59), compaction,
  `OpenTelemetryChatClient`, the provider's client behind a wrapper that never disposes it.
  The run's own wrapper above `OpenTelemetryChatClient` hands the provider's client each JSON result as text without
  white space (D45). The gate, checkpoints and compaction sit inside the tool loop, so they act around every model
  call. The mode
  layer builds a `FunctionInvokingChatClient` per pass, whose iteration limit is that pass's and whose error limit and
  concurrency are those of the call's options, so calls on one client never share a limit.
  The layers below the conversation's run only inside a run it began: the tool loop taken out of the created client
  with `GetService` and called on its own throws at the gate, where it would store nothing and count no model call.
  A provider's client that runs a tool loop of its own, as one registered with `UseFunctionInvocation()` does, is
  refused when the run's client is created, with an `InvalidOperationException` that names the call: its loop sits
  below the gate, the tool middleware and the run's limits, so tools ran past all three, and a tool that requires
  approval read "not approved" (D81). The check asks the client for a `FunctionInvokingChatClient`; a wrapper that
  answers no `GetService` hides the loop, and there the gate's refusal stays the last line. A summary's client is
  not checked, since a summary call offers no tools.
  The conversation layer writes a run's steps once for both transports: a streaming run hands on the updates of
  each pass and a run that does not stream whole responses, and everything around the pass is the same code. Two
  copies of those steps had drifted apart in what a failed run stores and in what a watcher receives.
- **Where:** `AgentHost.cs`, `Conversations/ConversationChatClient.cs` (the run's steps).

### D85. A run leaves the caller's synchronization context {#d85}

- **Status:** owner.
- **Context:** one await of the library configured how it resumes, and the code the generator writes awaited a tool
  plainly. A WPF, WinForms, MAUI or Blazor app that ran an agent from its UI thread had nearly every continuation of
  the run come back to that thread: a streamed run of 102 updates posted to its context hundreds of times, the store
  appended there, and blocking on a run there deadlocked. Configuring Typeshift's own awaits was not enough:
  Microsoft.Extensions.AI 10.10's tool loop and `OpenTelemetryChatClient`, which every run passes through (D29), await
  without `ConfigureAwait(false)`, and so does most of an app's code. A run that reached them before it waited, as
  one on the store in memory does, still posted 80 to 120 times for those 102 updates, and its tools and appends ran
  on the UI thread.
- **Decision:** every await in `src/Typeshift`, `await foreach` and `await using` included, and every await the
  generator writes uses `ConfigureAwait(false)`. CA2007 is an error for `src/Typeshift` (`.editorconfig`), and
  `ToolsetEmitter` writes a tool's await so. The await with `ConfigureAwaitOptions.SuppressThrowing` in
  `RunMiddlewareChatClient` leaves out `ContinueOnCapturedContext`, which is the same as `false`. On top, each public
  member that runs the app's code or Microsoft.Extensions.AI's clears the current synchronization context for its own
  steps (`SynchronizationContexts.Leave`): a run and the raw path's client, each step of a stream as the caller asks
  for the next update or leaves it, the settings members, the decisions members, `LoadConversationAsync`,
  `CreateChatClientAsync` and a store's `VerifyAsync`. An async method calls it first, and its builder puts the
  caller's context back when the method returns to its caller, so the caller's own awaits still come back to it.
  Nothing moves to another thread, and the execution context, with `Activity.Current` and every `AsyncLocal`, flows
  as before. A run posts nothing to the caller's context, and the tools, middleware, context providers, providers
  and store it calls see none (`SynchronizationContextTests`).
- **Alternatives:** keeping the caller's context on purpose and saying so, which leaves every update of a stream
  crossing a UI thread several times and blocking on a run there a deadlock; `ConfigureAwait(false)` alone, which
  holds only once a run waited in Typeshift's code and leaves the run on the store in memory on the UI thread; a hop
  to the thread pool where a run starts, a thread switch per run and per update that clearing the context avoids;
  configuring only the awaits on hot paths, without an analyzer, which drifts with every change.
- **Where:** `.editorconfig`, `src/Typeshift`, `Hosting/SynchronizationContexts.cs` and its callers,
  `Hosting/StreamingRun.cs`, `Conversations/ConversationChatClient.cs` (the raw path),
  `Emitting/ToolsetEmitter.cs` (`WriteInvoke`).

### D30. Middleware: one interface around runs, one around tool calls {#d30}

- **Status:** owner.
- **Decision:** `IRunMiddleware.InvokeAsync(run, next, ct)` serves streaming and non-streaming runs alike: it may
  change messages and options, read `run.Response` after `next`, or set it instead of calling `next`. Setting it
  once `next` was called throws: by then the answer is stored (D50), has streamed and was published (D47), so a
  response set there reached the caller of a run that does not stream and nobody else. What a model says is
  changed below the checkpoints, by a client the app wraps around the provider's, which the guide shows.
  `IToolMiddleware` wraps `FunctionInvocationContext`, and reads the run through `call.Run`, an extension property
  over the call's options, which are the run's and hold it, at the owner's choice: no implementation changes
  signature, where an `AgentRun` parameter would have broken every one. Registered in the chain, in order, for every
  agent; a middleware filters by `run.Agent`. Changes to the messages hold for the pass; the conversation keeps what the
  host sent. A response set without `next` is stored with the conversation, and a pass that called no model ends
  the run, storing the steering input that waits. In a streaming
  run the middleware awaits `next` while the updates go on to the caller through a channel, which reads the model's
  stream ahead of a slow caller. A steered run passes through again for steering input that arrives after its
  answer.
  Steering input joins a run below run middleware, inside the tool loop (D51), so `InvokeAsync` never has it among
  its messages. The interface has a second member for it, `OnSteeringAsync(run, input, ct)`, with a default that
  lets the input pass: every registered middleware sees steering input in order before the run takes it, and
  returns it, another message in its place, or null to drop it. What it returns is what the run takes, sent to the
  model and stored: unlike a run's input, nothing of it is stored before middleware sees it, and input that was
  dropped and stored all the same would reach the next run's model. Input that is dropped starts no pass. Input
  that waits at a run's end or its handoff is shown too before it is stored, and when a middleware throws there
  the input is left out and the failure logged, since the run's end must be stored and what no middleware judged
  must not reach a model. A background agent's answer is none of the user's input and is not shown.
- **Where:** `Middleware/`, `FunctionInvokingChatClient.FunctionInvoker` for tool middleware,
  `Conversations/ConversationRun.cs` (steering input).
- **Alternatives:** MAF's separate run and streaming delegates (streaming falls back to non-streaming when one is
  missing); per-agent middleware in the agent's chain, a second place; applying a response set after `next`
  everywhere, which takes checkpoints that wait for run middleware and a stream held back until it ends; a named
  output guardrail in the chain, applied below the checkpoints, which is a feature of its own and not built; for
  steering input, starting a pass for
  each one, so that `InvokeAsync` finds it as the pass's last message, which would keep it from the next model
  call inside a tool loop (D51); and leaving the check to the app, before it calls `TrySteer`, a second place for
  every guardrail.

### D31. Context providers {#d31}

- **Status:** approved.
- **Decision:** `UseContext<T>()`; `IContextProvider.GetContextAsync(run, ct)` returns an `AgentContext` of
  instructions, messages and tools, appended to the run's. Called after resume, before the model, on each pass of
  a run (a steered run makes more than one); its messages are never stored. Providers are resolved from the run's
  services, or created from them when none is registered. One created is the pass's: a new one for each pass,
  disposed once the pass ended, since the tools it returned run until then; one that keeps state or resources
  between passes is registered as scoped, and its scope disposes it. Before, a created provider was never disposed,
  and leaked what it held once per pass. No after-run hook: run middleware covers it.
- **Where:** `Context/`.

### D32. Run options, with compaction inside the tool loop {#d32}

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
  schemas, each call, JSON result and schema written out once a run and its length kept by reference, since a run
  estimates its whole history before each model call (7.9 MB for one estimate of 2,000 messages before), calibrated by the tokens the provider reported for the run's last call against the estimate of that call
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
  message it stands for, since compaction sees no other; pinning it would leave that turn in no summary. A summary
  call's tokens are the run's, which its response reports and the summary's entry holds, as a sub-agent's are
  (D33); a model response's entry keeps its provider's report alone, so compaction's first estimate reads no
  summary's tokens as a call's. A summary is best effort (owner): a summary call that fails is logged as a warning
  and recorded on the run's span, the run's own
  provider and model write the summary when another was named (D77), and when that fails too the call goes out with
  its results cleared and no summary, for the provider to answer or reject. Before, the failure ended the run ahead of
  its own model call, and every later run of the conversation, since each summarizes first. The run's cancellation
  still ends it. What compaction did holds for the rest of the run: the tool loop keeps the whole
  history, so each later call gets the same cleared results and summary. The summary entry holds the summary, the
  kept todo list and copies of the kept messages, with the background calls still working, so a
  later run loads only that entry and the ones after it (`LoadConversationSinceCompactionAsync`, which a store may
  override to leave the rest unread). The mode and the standing approvals are no part of it: they are in the
  conversation's decisions document, which no summary touches (D52, D80). The passes steering input or background answers start share
  `MaxToolIterations`: each gets what the run has left. A call a rule, a standing approval or auto mode approves is
  a round like any other, since the gate decides it inside the loop (D81). Calls your app approves run in the
  conversation's next run, which counts its own rounds; carrying them out takes one turn of the loop that calls no
  model.
- **Ranges (owner: traps are loud):** a run needs one round of tool calls or more, no negative count of failed
  rounds, a maximum and a target above 0 and a target below the maximum. A value outside is refused where it enters,
  by the option's name: the chain throws from the agent's constructor, a save stores nothing, and a run whose layers
  resolve one throws before it starts. Before, the first check was the setter of `FunctionInvokingChatClient` inside
  the tool loop, whose message named a parameter `value`, so stored settings of no round saved and then failed every
  run of their id. A save checks the layer over no values but its own (`AgentRuns.SaveSettingsAsync`). Checked over
  the agent's code, it would refuse a target that the agent's own stored settings, the layer between, make valid;
  what goes wrong only between two layers is the resolving run's to report.
- **The summary's instructions:** `WithSummaryInstructions(text)` and `RunOptions.SummaryInstructions` replace the
  summary model's English instructions, a value in the one chain, configurable as the summary model is. A strategy
  of the app's own for the summary was rejected above; a text is none. The summary's prefix, which the run's own
  model reads, stays Typeshift's.
- **Where:** `Compaction/`, `Conversations/ConversationEntry.cs` for the stored estimate,
  `Conversations/ConversationRun.cs` for what a pass added, `Approval/ModeChatClient.cs` for the shared limit,
  `Agents/RunOptions.cs` and `Hosting/ResolvedChatOptions.cs` for the ranges.
- **Alternatives:** MAF's strategy pipeline and trigger combinators, more concepts for the same two steps; a
  tokenizer dependency; message-count triggers, which ignore that one tool result may outweigh a hundred messages; a
  separate `WithCompaction()` chain next to the limits. For a summary call that fails: ending the run, as it did,
  which let a summary model that was down fail every later run of a conversation; refusing only a summary model the
  code named on a provider the user switched to, which covers one of the two causes and needs the generator to say
  which layer named the model. For what a pass added: leaving every message the conversation never stores out of the
  summary, which loses the turns whose messages run middleware replaced.

### D33. Sub-agents, in the foreground or the background {#d33}

- **Status:** owner.
- **Decision:** `UseAgent<T>()` offers a tool named like the agent, with its description and one `input` string. A
  call runs it in a fresh context with the same settings id, the caller's mode, standing approvals and rules, and no
  conversation of its own, and returns its text, or its JSON with `.WithResult<T>()` (D65). `.InBackground()` makes the tool return at once; the answer reaches
  the calling run as a message before a later model call, and the run does not end while sub-agents work. A
  sub-agent cannot ask your app: in the default mode its calls that need approval and that nothing approves are
  rejected, and its model reads the rejection as the call's result (D81; TSH024 warns), and it is not offered
  `ask_user` (D82; TSH034 warns). Circles are TSH026.
- **As built:** the sub-agent's tool is a runtime `AIFunction` that builds the agent's `AgentHost<T>` over the
  caller's services through a factory `UseAgent<T>()` captured (D76), so no generated code is needed; it finds its caller's run through
  `FunctionInvokingChatClient.CurrentContext`. Background answers join the run through the steering queue, so
  every run has a `ConversationRun`, which stores only with a conversation. Such an answer is a message in the
  user's role, the one a run takes between model calls, with the sub-agent's name as its author, a first line that
  tells the model it is no message from the user, and a mark that `IsBackgroundAnswer` reads, since what the
  sub-agent read must not pass for the user's words, to the model or in a chat view built from the store; the run's end cancels the sub-agents
  still working and awaits them, so none outlives its run or the scope of a host disposed after it (D76), and the
  next run tells the model which ones it lost, after a crash, a cancellation or a failure,
  including the ones a summary left working. A handoff ends the handing agent's part of the run, so it stops them
  the same way (`StopBackgroundAsync`) and stores, for the agent handed to, a message for each one that never
  answered; an answer that arrived before the handoff is stored for that agent as steering input is. A sub-agent
  that fails tells the calling model only that it failed, unless it failed with a `ToolException` (D53). What a
  sub-agent's run spent counts as its caller's (owner: everything a run spent): the caller's `AgentRun` adds the
  usage of each sub-agent's response, which holds that sub-agent's own sub-agents', and the caller's response
  reports it beside its model calls', a streaming run in an update of its own before its decisions. The caller's
  next entry that is no model response holds it, so the entries add up to what the responses report. Before, the
  response and the entries counted the caller's own model calls only, 20 of 140 input tokens in a measurement, while
  an app billed from them. TSH023 and
  TSH024 check the agents a compilation uses, a reference's
  through its module stamp, which carries the agent's name, whether it has a description and the tools it offers that
  require approval; tool stamps carry `RequiresApproval` for that list. TSH026 checks the compilation's own agents,
  since assemblies cannot reference each other in a circle.
- **Where:** `Agents/SubAgentFunction.cs`, `Conversations/ConversationRun.cs`, `Hosting/IAgentHost.cs` (the
  `RunHandover` a sub-agent's run is handed, with its caller), `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** passing a sub-agent's decisions up to the caller's conversation, which pauses a parent run on a
  child's call and needs the child's state stored to resume (possible later); MAF's six background-task tools
  (start, wait, results, continue, clear), where one tool and an injected answer do the job; storing each sub-agent
  run as a conversation of its own, which needs ids the library may not create.

### D34. Handoff continues with the agent handed to {#d34}

- **Status:** owner.
- **Decision:** `UseHandoff<T>()` adds a declaration-only tool `handoff_to_{name}`. Calling it ends the current
  agent's loop; the target answers in the same run with the whole conversation. The conversation's last entry names
  the agent that answers its next run, if it is reachable through the host agent's handoffs. Handoff needs a
  conversation; run settings apply to the host's agent only, except a mode they name, which is the conversation's
  (D52) and so reaches the agent that answers or is handed to. A run hands off at most ten times: each agent
  handed the run has a limit of model calls of its own (D32), so nothing else bounds two agents that hand back and
  forth. A handoff beyond that is answered as refused, and the agent that called it goes on. A handoff called in a
  response of which a call waits
  for your app is not carried out: the model reads that it was not handed off, and the agent that asked keeps the
  conversation, so the answers continue with it (D81).
- **As built:** the conversation layer sees the declaration's call the tool loop passes out, stores its result as an
  entry of the agent handed to, and lets that agent resume the run; a run without a conversation does not offer
  handoffs, and a call of an agent it does not hand off to is answered by the tool loop as an unknown tool. The tool
  loop hands a whole response back uninvoked when one of its calls names a declaration, so the calls a model makes
  beside a handoff never run. `HandOffAsync` answers each of them in the handoff's own tool message, as not run
  because the conversation was handed off, and so the agent handed to reads them; of two handoff calls the last
  is carried out. Running them instead would take a tool loop of Typeshift's own. Tool results
  after which the tool loop made no model call are stored before the run ends, a handoff's and a terminating tool's
  alike. The handing agent's run swaps its claim on the conversation (D50) for a claim of the agent handed to, and
  passes it to that agent's run, as a run passed on to the agent that answers the conversation is passed its claim;
  so no run starts between the two. A run that starts then waits, as for any run still loading, and is refused once
  the agent handed to began. Who answers is decided before anyone's options resolve: a host's run takes its
  conversation first (`RunStart`: the claim, the load from the last summary, a mode its settings name), and on a
  conversation another agent answers it passes the run on with the loaded entries. So the conversation is loaded
  once, and the agent the run was started on loads no settings, lists no MCP server and asks for no chat client,
  whose failure would otherwise fail a run it does not answer. What one run hands another is one record,
  `RunHandover`: the settings id, the conversation, the claim, a typed run's format (D64), the handoffs the run made
  so far and, for a run passed on, the loaded entries. It travels as an argument and in the run's options rather
  than in an `AsyncLocal`, which a value set before a stream's `yield` loses and code the agent's run calls would
  carry along. The limit of ten handoffs is a constant: a run option for it would be one more member of every
  agent's generated settings, for a number nobody has asked to change.
- **Where:** `Conversations/ConversationChatClient.cs`, `Conversations/RunStart.cs`, `Conversations/LiveRuns.cs`,
  `Hosting/IAgentHost.cs` (`RunHandover`), `Hosting/AgentHost.cs`.

### D65. A sub-agent can answer in a type {#d65}

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

### D35. MCP servers through a host provider {#d35}

- **Status:** approved.
- **Decision:** `UseMcpServer(name)` with `.RequiresApproval()`; the host's `IMcpToolProvider` returns the server's
  tools, connected with the MCP client of its choice. Typeshift takes no MCP dependency. Provider-side servers use
  `HostedMcpServerTool` in `WithTools`.
- **As built:** the servers' tools are asked for when a run's options are resolved, after the agent's own;
  `ReadOnly()` marks them for plan mode, `RequiresApproval()` wraps them in `ApprovalRequiredAIFunction`. A tool
  named like the agent's own fails the run.
- **Names at run time:** a run's tools come from more places than a declaration shows: the chat options
  (`WithTools`), MCP servers and context providers, beside the toolsets, the agent's own tools, its sub-agents and
  its handoffs. Before each pass, where the list is complete, `OfferedTools.Check` fails the run when two functions
  share a name, naming where each comes from, and when a tool has a name the run offers itself: `ask_user`,
  `exit_plan_mode`, or one that starts with `handoff_to_` and is no handoff of the agent. The analyzer reports the
  same for what declarations show: TSH035 for a toolset's or an agent's tool of such a name, TSH025 for a sub-agent
  named like a tool or like another sub-agent, TSH023 for a sub-agent of such a name. The generator holds the three
  literals itself, and `ReservedToolNameTests` compares them with the runtime's.
- **Where:** `Mcp/Mcp.cs`, `Hosting/AgentHost.cs`, `Hosting/OfferedTools.cs`, `Parsing/ToolNames.cs`.

### D36. OpenTelemetry is always on {#d36}

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
- **Decisions:** a run that ends asking your app is no error. Its span counts what it asked in
  `typeshift.decisions.asked` and carries a `typeshift.decision.asked` event for each decision, added once the
  batch is saved; a batch that could not be saved fails the run and its span. The run that settles a batch adds a
  `typeshift.decision.settled` event for each call it gives a result: the one that carries the answers out, the one
  whose input expired the batch, or the one that repairs a batch another run started. Each names the call, the
  decision's id and kind, and an outcome (`approved`, `rejected`, `answered`, `declined`, `run`, `expired`,
  `unknown`). The call's arguments and the text the model reads in a call's place are content, so they join the
  events only where the run's chat spans carry their messages.
- **One per backend:** a backend is what the instrumentation reads once from a client's `ChatClientMetadata` and
  writes on every span and metric: the provider's name, the server's address and port, and the client's own model.
  That model names only the calls that name none, so it counts only for a run without a model, and the clients a
  provider makes per model share one. One per provider and model of the run, as before, kept an instrumented client,
  with its meter and four instruments, for every model name and provider id an app ever used, for ever where they
  come from data, and gave all the clients under one provider id the metadata of the first. No entry is removed or
  capped, since that would dispose a meter; the backends an app talks to bound them. Each is made once, under a
  `Lazy`, so two runs that reach a new backend together leave no second meter behind.
- **The provider a run resolved (owner: traps are loud):** a chat span names its backend as the client describes
  itself, which says nothing of the provider id the agent's code or its settings named. The run's span carries that
  id as `typeshift.provider`, `AgentRun.Provider` hands run middleware its value, and the entry of each model
  response keeps it as text beside the model (D50), written the same in every culture. A run on the default client
  (D63) names none, so the tag and the entry's member are absent, as on entries stored before.
- **Streaming:** an async iterator resumes in its caller's context (dotnet/runtime#47802), so after a streaming run's
  first update its span was no longer the current one, and the later chat and tool spans hung under the caller's span
  or started traces of their own. The run makes its span current again before it asks for each update, as
  Microsoft.Extensions.AI's tool loop does; the caller's current span stays its own.
- **Tool arguments and results:** under an `invoke_agent` span the tool loop asks that span, not its client, whether
  to write them: it looks for the custom property `__EnableSensitiveData__`. The run's span carries it when the run's
  `OpenTelemetryChatClient` captures content (`EnableSensitiveData`, which the variable sets), so tool spans carry
  content exactly when chat spans do. The key is internal to Microsoft.Extensions.AI;
  `TelemetryTests.Given_Message_Capture_A_Run_Tags_The_Tool_Span_With_Arguments_And_Result` fails when it changes.
- **Logs:** what Typeshift logs itself is one category, `Typeshift`, and a `LoggerMessage` event for each thing it
  says, with an id and a name that stay: warnings and errors for what a run survives (a failed append, a failed
  summary, a content it stored or read without, a background sub-agent that failed, a watcher that lost updates,
  the first write without a store), and `Debug` events for why a run did what it did (the model and provider it
  resolved with the settings that applied, a compaction, an approval rule's verdict, a handoff). Up to
  0.1.0-alpha.6 the entries had the event id 0 and the names of internal classes as categories, so an alert had to
  match text. A new event takes the next number, the guide lists every event, and `LogEventCatalogTests` holds the
  list to the code. The tool loop logs a tool's exception under Microsoft.Extensions.AI's own category. The logger is the
  `ILoggerFactory` of the run's services, none when the app registered none (D75). A failed run's span carries
  its exception as an event, as the span of a run that survived a store's failure does.
- **Where:** `Telemetry/Telemetry.cs`, `Telemetry/InstrumentedClients.cs`, `Telemetry/LogEvents.cs`,
  `Conversations/ConversationChatClient.cs` (`GetStreamingResponseAsync`).

### D37. No built-in toolsets {#d37}

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

### D53. A failed tool call tells the model what was written for it {#d53}

- **Status:** owner.
- **Context:** Microsoft.Extensions.AI's tool loop hides why a call failed unless `IncludeDetailedErrors` is on, so
  the model read only "Error: Function failed." and could not correct a missing argument or an unknown id. Up to
  0.1.0-alpha.6 the loop therefore passed every exception's message on. Most exceptions a tool lets through were
  written by a database driver, an HTTP client or the container, for a developer: they named hosts, queries and
  connection strings, and went to the model provider, into the response, into the stored conversation and to its
  watchers.
- **Decision:** `ToolException` is the exception whose message is written for the model. The model reads it as the
  call's result, behind the prefix Microsoft.Extensions.AI writes. Of any other exception it reads one fixed
  sentence, `ToolLoop.Failed`. What Typeshift itself tells the model is a `ToolException`: an argument that does
  not fit, below, and a sub-agent's answer outside its result type (D65). A background sub-agent's failure (D33)
  follows the same rule in the message that reports it, and is logged and recorded on the calling run's span.
- **The exception is never lost:** `ToolLoop` derives from `FunctionInvokingChatClient` and overrides
  `CreateResponseMessages`. After the base wrote the results, it replaces the result of each call whose exception is
  no `ToolException`. Nothing is caught, wrapped or thrown again, so the loop logs the exception the tool threw as
  an error, sets its type and message on the call's `execute_tool` span, keeps it on the call's
  `FunctionResultContent.Exception`, which is never serialized, counts it for `MaxConsecutiveToolErrors` and, past
  that limit, ends the run with it as it is.
- **Arguments:** a call the model got wrong fails before the method runs, with a message that names the tool and
  the argument. An argument holds what its schema requires: a required one may not be missing or, unless the
  parameter is nullable or a union (D62), null, and a record's member may not be left out without a default or be
  null where its type takes none. Arguments are therefore read with options of their own, the tool JSON options plus
  `RespectNullableAnnotations` and `RespectRequiredConstructorParameters`; results keep the plain ones, since the
  annotations also fail a null being written. A serializer failure is wrapped with the argument's and the tool's
  name. An object sent as a JSON string is read as its JSON only after the string itself did not bind, so a text
  that starts with `[` still binds a string, also as a case of a union.
- **Where:** `Toolsets/ToolException.cs`, `Hosting/ToolLoop.cs`, `Hosting/AgentHost.cs`,
  `Generated/GeneratedToolFunction.cs`, `Generated/ToolJson.cs`, `Agents/SubAgentResult.cs`,
  `Conversations/ConversationRun.cs`.
- **Alternatives:** every exception's message to the model, as before, which sends a third party text nobody wrote
  for it; a run option that passes every message on again, which is the same forwarding behind a switch, and three
  lines of tool middleware for an app that wants it; an exception with the fixed sentence thrown from the function
  invoker in the tool's place, which hands the app a wrapper past the limit and puts the wrapper on the span;
  catching exceptions in tool middleware and returning a result, which hides the failure from
  `MaxConsecutiveToolErrors`.

## Storage

### D38. A store of documents and a store of conversations {#d38}

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
- **One store per interface:** up to 0.1.0-alpha.6 the last call won for each interface. With D69, where any store
  ends the memory, that made the order of two calls decide where data went, without a word: `WithFileStore()` and
  then `WithStore<InMemoryTypeshiftStore<Guid>>()`, the natural repair for "no conversation store", moved the
  agents' own settings back into memory, and the memory store added after a database store of the same id type
  took its settings and conversations. The owner chose the throw: a chain call whose store implements an interface
  that another call's store is registered as throws an `InvalidOperationException` at registration, naming both
  stores and the interfaces, and registers nothing. `replace: true` on a store call says the store takes over each
  interface it implements, for a test host that swaps a database store for one in memory; the replaced store keeps
  the interfaces the new one does not implement. A store is its declared type and what tells it from another of
  that type: the instance, the factory, the file store's folder, nothing for one the container builds, so the same
  store registered twice is one store. The chain's claims live among the registrations, as an instance
  (`StoreClaims`), so every chain `AddTypeshift()` returns for one service collection sees them. A store your app
  registers in the container itself is no chain call and follows the container's rule, the last registration
  wins, which a test host's `ConfigureTestServices` relies on.
- **The memory store yields agents' own settings:** `InMemoryTypeshiftStore<TId>` implements `IAgentSettingsStore`
  for every id type, so it would collide with every other store of them. It is registered as that interface only
  while no other store is, a chain's or one the app registered before, and a store registered later takes the
  interface from it without `replace`. So the file store and the memory store compose in either order, and of
  several memory stores the first keeps agents' own settings. With `replace: true` it takes them like any store.
- **Where:** `Storage/ISettingsStore.cs`, `Storage/IConversationStore.cs`, `Storage/ITypeshiftStore.cs`,
  `Storage/StoreClaims.cs`, `DependencyInjection/TypeshiftBuilder.cs`.
- **Alternatives:** v0's load and save only, which rewrote a whole conversation per turn; a generic append for any kind,
  a concept with one user; `LoadAsync<TData>` and `SaveAsync<TData>` over `IStorable` documents (up to 0.1.0-alpha.3),
  which made every store call `TData.Kind`, `Serialize` and `Deserialize` itself; one document per settings id holding
  every agent's settings, where a save of one agent's settings would conflict with a save of another's (D41); one
  interface for both (up to 0.1.0-alpha.4), which made a store of conversations implement settings methods it does not
  keep; `WithSettingsStore<T>()` and `WithConversationStore<T>()`, two calls where the interfaces already say what a
  store keeps; renaming the conversation methods (`LoadAsync`, `AppendAsync`), which would clash with the settings
  methods in a class that implements both; keeping the last call as the winner and logging a warning when a memory
  store displaces another, which compiles an order that loses data down to a log line; taking
  `IAgentSettingsStore` off `InMemoryTypeshiftStore<TId>` into a store of its own, a third type where the two
  built-in stores can compose.

### D66. Each agent has settings of its own, under no id {#d66}

- **Status:** owner.
- **Context:** the owner asked for "a configurable, storable agent settings record for a single agent", stored "keyed
  by the kind" alone, so that a simple app needs no settings ids.
- **Decision:** `IAgentSettingsStore` keeps a document per kind, with a version, and no id.
  `UpdateSettingsAsync(update)`, `LoadSettingsAsync()` and `SaveSettingsAsync(settings, loadedVersion)` on a host
  without a settings id are the members of D41 for them. Every run of the agent applies its own settings, over its
  code and under the settings of a settings id, so they are the stored layer of an app without settings ids and the
  defaults of every id in one with them. `InMemoryTypeshiftStore`
  implements the interface, and `WithStore<T>()` registers it as it does the other two, the memory store only while
  no other store keeps them (D38). A run loads them only
  when a store of them is registered, so a store without them leaves runs as they were. Loading or saving them
  without one throws, naming `IAgentSettingsStore`. They have paths of their own down to the store. Since D72 no
  member takes an id: the settings members of a host change the agent's own settings unless the host came from
  `WithSettingsId`, and TSH041 reports a `WithSettingsId` whose copy is dropped, the slip that would reach them
  unmeant. Asked whether a host without a settings id should refuse them, the owner kept them: "Settings without an
  ID can be seen as the default global settings, configurable at runtime. Developers may or may not block access
  based on authentication state of their user."
- **Where:** `Storage/IAgentSettingsStore.cs`, `Storage/StoreAccess.cs`, `Storage/StoreExtensions.cs`,
  `Storage/InMemoryTypeshiftStore.cs`, `Hosting/AgentHost.cs`, `Generated/AgentRuns.cs`,
  `Generated/ModuleRegistration.cs`, `DependencyInjection/TypeshiftBuilder.cs`, `Emitting/AgentEmitter.cs`,
  `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** the agent's own settings in place of a settings id's instead of under them, which leaves an app
  with ids no stored defaults; a reserved id, such as `Guid.Empty`, which not every id type has and a store cannot
  tell from a real one; two more methods on `ITypeshiftStore<TId>`, which every store implementing it would have to
  add; settings without an id on `ISettingsStore<TId>`, which ties them to an id type; a null settings id for them,
  which a missing id of a reference type would reach; members of other names, such as `LoadOwnSettingsAsync`, which
  keep `LoadSettingsAsync(default)` meaning an id but give one record two sets of names; settings members that throw
  on a host without a settings id, or an option that makes them throw, which the owner turned down, since who may
  change the defaults is the app's to decide.

### D67. A store of agents' own settings in files {#d67}

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
  unfinished when their process died. `WithFileStore(configure)`, a member of the chain like every other call,
  and `FileStoreOptions` are the only public parts, and `Folder` the only option. It defaults to `typeshift` under
  `AppContext.BaseDirectory`, which a service finds whatever its current directory, resolves at registration, and a
  blank one throws there, naming the option. That default lies in the output of the build or the publish, so
  another configuration, a cleaned `bin` folder, a publish to a new folder and a new container image start
  without the saved settings: the owner kept it, the guide says so in a warning, and the store logs its folder at
  the first save. The settings get the fixed `settings` folder in it, so a store of other data can take a folder beside it. The
  container builds the internal `FileAgentSettingsStore` over that folder with the app's `TimeProvider`,
  through the registration `WithStore<T>()` uses, so another store of agents' own settings beside it throws (D38):
  a file store of another folder too, while the same folder twice is one store, and the memory store leaves them
  to the files. `WithStore(store)` registers a store your code created, as each interface the instance implements,
  and the container leaves it undisposed.
- **Where:** `Storage/FileAgentSettingsStore.cs`, `Storage/FileStoreOptions.cs`,
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
  `WithStore(store)`, which made every app create a class of Typeshift's and hand it the clock itself; a default
  under `LocalApplicationData` and the entry assembly's name, which the owner turned down: it moves the settings of
  apps that rely on today's place, and a container loses either folder without a volume; `WithFileStore` as an
  extension method in `Microsoft.Extensions.DependencyInjection` (up to 0.1.0-alpha.6), which a file around
  `TypeshiftRuntime.Create` with a `using` of Typeshift alone did not find (CS1061).

### D68. The package turns XML docs on {#d68}

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
- **A project that turns docs off:** the compiler keeps doc comments as plain comments then, and writes no XML of
  them. The parser reads that text as the XML it is (`XmlDocs.ReadPlain`), so a tool's summary and `<param>` docs, an
  agent's summary and a settings member's summary describe them as with docs on; a cref reads as it is written,
  since nothing binds it. TSH011 named every tool and agent with docs before, whose descriptions were lost, a project
  setting the one place a description still hung on. It now names a tool's parameter whose type has docs, which
  reach the schema only through the XML Weboost.SchemaAnnotations collects (D55). `XmlDocsTests` reads every
  catalog both ways and finds no difference but the crefs.
- **Alternatives:** the tutorial's `Directory.Build.props`, a step of ceremony; docs on without hiding CS1591, which
  fails every build that treats warnings as errors; the `NoWarn` in the props, which hid CS1591 in every project
  that turned docs on in its project file; a test for an empty property in the targets, where the SDK has already
  set it to `false`; the XML file in the output folder, which every app that references the project would publish.

### D70. Instructions that never change are one call {#d70}

- **Status:** owner.
- **Context:** the owner asked for less ceremony. Constant instructions took an override of `GetInstructions()`, three
  lines outside the constructor that holds everything else an agent declares.
- **Decision:** `WithInstructions(text)` in the constructor sets the instructions, and `GetInstructions(services)`
  returns them unless the agent overrides it. The call runs when the agent is built, as the chat options' calls do,
  so the text need not be a constant. An override still wins, for instructions built per run from the run's
  services (D21).
- **Where:** `Agents/Agent.cs`.
- **Alternatives:** `WithChatOptions().WithInstructions(...)`, beside the model, which would split the instructions
  between `ChatOptions` and the override; reading them from the agent's XML docs, which already describe it to other
  agents (D14).

### D71. Every conversation store implements the load from the last summary {#d71}

- **Status:** owner.
- **Context:** the owner asked that the code, not only the docs, lead a store's author to the load runs call. It had a
  default on the interface that read the whole conversation, so a store that never knew of it read every entry on
  every run, however long compaction had let the conversation grow.
- **Decision:** `LoadConversationSinceCompactionAsync` has no default, so a store that leaves it out fails to build,
  and the IDE's fix inserts it with the XML docs that say what it returns. `entries.SinceLastSummary()`, an extension
  on the entries, is the one-line body of a store that can only read whole conversations. The store in memory, the
  default of an app without a store (D69), keeps where its last summary is as it appends it and reads from there: it
  read every entry before, 15.7 MB to return 3 entries of 5,000.
- **Where:** `Storage/IConversationStore.cs`, `Storage/ConversationEntries.cs`, `Storage/InMemoryTypeshiftStore.cs`.
- **Alternatives:** the default with an analyzer warning on `WithStore<T>()`, which can be suppressed, and whose "does
  any agent compact" a compilation cannot always answer for agents of other projects; only the docs, which the owner
  found not enough; dropping `LoadConversationAsync` instead, which a chat UI needs for the whole history.

### D39. Kinds are stable names {#d39}

- **Status:** owner.
- **Decision:** `IStorable.Kind`: for an agent's `Settings` its `WithStorageKind("...")` if it has one, or else its
  class name; `conversation_entry` for conversation entries. Moving an agent to another namespace keeps its data;
  renaming the class keeps it once `WithStorageKind` pins the old name. Two agents of one kind are TSH022, and a
  kind that is empty or blank is TSH039, since a store keeps the settings under it.
- **Alternatives:** the agent's name, which would need unique names (out of scope); the full type name (v0).

### D40. A schema version per document {#d40}

- **Status:** owner.
- **Decision:** `IStorable.SchemaVersion`, 1 for `Settings` until `WithSchemaVersion(n)` raises it. The JSON carries it
  as `"$schemaVersion"`, left out at 1, and `Deserialize` refuses a newer one; migrating older documents is the app's
  job, in its store or its database. A version below 1 is TSH039: its documents would name none, read back as
  version 1 and be refused as newer than the code that wrote them.
- **Details:** a store keeps the JSON and a version and nothing next to them. A schema version beside the JSON gave
  every store a column and every save a parameter, and a JSON library that read the JSON alone never saw it.
  `ConversationEntry` carries its schema version the same way, and `System.Text.Json` and Newtonsoft.Json check it too,
  where they used to read every entry as the current one. JSON written before the schema version moved names none and
  reads as 1; a `"$schemaVersion"` that is no whole number is refused, since it cannot be shown to be older.
- **Alternatives:** a hash of the record's shape (automatic, but no store can map it to a migration); a
  migration hook on the agent; an analyzer against a shipped list of stored members (later); the schema version next to
  the JSON (up to 0.1.0-alpha.4), a column every store kept; no schema version at all, which would read settings a newer
  release wrote after a rollback.

### D41. Optimistic concurrency {#d41}

- **Status:** owner.
- **Decision:** `SaveAsync` takes the version the caller loaded as `loadedVersion`, 0 for none, and throws
  `StoreConflictException` when the stored one differs. `UpdateSettingsAsync` reloads and reapplies until it wins
  (each conflict means another save won), at most ten times, so a store that conflicts every time fails instead of
  spinning. `SaveSettingsAsync` takes the version from `LoadSettingsAsync` or `UpdateSettingsAsync`.
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

### D58. A store's own tests check it against the contract {#d58}

- **Status:** owner; the second id, the entries' times and the save of a document that is not stored came with the
  audit of November 2026, and the refusal of a taken number every store owes with 0.1.0-alpha.8.
- **Decision:** `store.VerifyAsync(id, otherId)`, an extension method on `ISettingsStore`, `IConversationStore`
  and `IDecisionStore`, runs the rules Typeshift relies on against a store and throws an
  `InvalidOperationException` that names the rule a store broke. It runs the rules of every interface the store
  implements for ids of that type, whichever one it is called through, so a store that is both cannot pass on one
  alone. A store of several interfaces matches the method of each, which the compiler called ambiguous (CS0121)
  unless the store was an `ITypeshiftStore`, whose own method was the most specific: a store of conversations and
  their decisions, the split the guide recommends, did not compile. The three methods carry an
  `OverloadResolutionPriority`, settings before conversations before decisions, so one is chosen for any store, and
  the method on `ITypeshiftStore`, which ran the same rules, went. It needs no test framework,
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
  and two equal ids throw an `ArgumentException`. Entries carry numbers as a run gives them (D50), and the other
  conversation holds the first entry's number, which no store may refuse. Last, an entry under a number its
  conversation holds must be refused with a `StoreConflictException` and nothing stored (D50): storing it fails, and
  so does any other exception, since a run takes it for a failed save and goes on.
- **Where:** `Storage/StoreExtensions.cs`.
- **Alternatives:** `StoreCheck.CheckSettingsAsync` and `CheckConversationsAsync`, two static methods named for the
  interface rather than called on the store, which the owner found clunky and which let a store that is both pass on
  one; one named case per rule for a `[Theory]`, which adds API and leans on a test framework; an abstract xUnit test
  class in a package of its own, which ties stores to one test framework; a checking decorator around the registered
  store, which finds a broken rule only when a run hits it; `VerifyAsync(id)` alone (up to 0.1.0-alpha.6) or kept
  beside the call with two ids, where the short call passes a store that mixes customers; a second id the check
  makes itself, which no id type of the app's choosing offers; new kinds per call for agents' own documents (up to
  0.1.0-alpha.6), which left documents behind under names nobody could predict; entries with rising times, which a
  store that orders by time passes; for the taken number, passing a store that refuses it or stores it alike, which
  passes a store whose unique key a migration forgot, and a flag of the store's, `RefusesTakenSequences`, under
  which the check looked (tagged 0.1.0-alpha.7, never published), which passed a store that never set it.

### D42. Settings ids and conversation ids are separate; conversations grow by appending {#d42}

- **Status:** owner.
- **Context:** the first draft keyed conversations by the run's `id`, its settings id, as their owner. The owner
  found `id: customer` confusing: one id named whose settings apply and who owns a conversation.
- **Decision:** runs take `settingsId` (which stored `Settings` apply) and `conversationId` (which conversation
  resumes), independent of each other; either may be left out. What a settings id stands for (a customer, a team, a
  user, a profile) is the host's choice. A conversation is keyed by its id alone: the host authorizes it before
  passing it, as any id from a request, and the guide says so. Runs append entries (D50); no locks, no second
  read. The store refuses a second run's entry by its number in the insert itself, with a unique key, so that holds
  there too.
- **Alternatives:** an explicit `ownerId` on conversation runs, a third id whose authorization the host still owns;
  keying conversations by settings id, which ties two unrelated choices of the host.

### D43. The store is the persistence switch {#d43}

- **Status:** carried (v0 D26), changed by D69. Once a store is registered, everything that loads or saves through a
  store interface none of the registered stores implements throws, naming `WithStore<T>()` and the interface (D38).
  Without any store, D69 keeps everything in memory. A load of decisions is the exception: without a store of them it
  answers "none", and only a write throws (D79).

### D69. Without a store, everything is kept in memory {#d69}

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
  the first run is asked for again by the next. A message for a missing store ends with the interfaces the app
  did register, and says that nothing is kept in memory beside them, since an app that registered only the file
  store finds its runs with ids throwing. The message for missing agent settings names `WithFileStore()` and a
  store of the app's own; it named `InMemoryTypeshiftStore<TId>`, which keeps nothing across a restart.
- **Where:** `Storage/StoreAccess.cs`, `Generated/ModuleRegistration.cs`.
- **Alternatives:** throwing as before, ceremony every first run hits; memory for each interface no registered store
  implements, which hides a forgotten store in an app that chose one; a warning at startup, which an app that never
  stores anything would see too; asking the container for the stores of the build's id type, which went with that
  type (D72); `WithStore<T>()` alone as the switch, which kept the settings of an app that registered its store of
  conversations itself in memory, under a warning that no store was registered; resolving a scoped store from the
  run's scope, which gives up the one instance that D25 and the store guide plan for, a second way to write a store.

### D79. Decisions have a store of their own, and a setup that would lose them throws {#d79}

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
  pays nothing, and the first write (a batch, a standing approval, a mode) throws, naming `IDecisionStore<TId>` and
  `WithStore<T>()`. Nothing is kept in
  memory beside a real store. TSH036 warns at build time about a `WithStore<T>()` whose store keeps conversations
  of an id type no store of the same chain keeps decisions for, when an agent the project sees can ask: one with a
  tool or an MCP server that requires approval, with plan mode in code or a configurable mode, or with
  `UseQuestions()`, a referenced agent by its stamp. It reads one chain, a fluent one or the calls on one variable,
  so a store registered elsewhere is unknown to it, and the run-time exception stays.
- **Where:** `Storage/IDecisionStore.cs`, `Storage/StoreAccess.cs`, `Storage/StoreId.cs`,
  `Storage/StoreExtensions.cs`.
- **Alternatives:** a kind of their own on `ISettingsStore<TId>`, which needs nothing new from a full store but
  shares the settings' id space: settings ids and conversation ids are separate, often of different types, so
  conversation 7's decisions would sit among customer 7's settings, where a delete or a tenant filter meant for
  settings reaches them; both, through an adapter the app opts into, a second way to do one thing; memory for
  decisions beside a registered conversation store, which loses what a run asked on every restart while the
  conversation stays.

### D72. Ids are given in code, and their types pick the stores {#d72}

- **Status:** owner.
- **Context:** the owner asked that the id type move from the build into code and become "more granular": settings
  and conversations of one app may have ids of different types. One MSBuild property typed every id of the build
  (D11).
- **Decision:** `host.WithSettingsId(id)` and `host.WithConversationId(id)` return a copy of the host that carries the
  id, typed by the call, so `WithSettingsId(42)` names an `ISettingsStore<int>` and `WithConversationId(chat)` an
  `IConversationStore<Guid>`. The id goes down the run as a `StoreId<TId>`, which reaches the store of its type through
  a generic call, so no type is made at run time and Native AOT keeps working; it is equal only to an id of the same
  type and value, so conversation 5 as an `int` and as a `long` are two conversations. An id of a nullable value type is
  its value: `WithSettingsId<TId>(TId? id) where TId : struct` and its twin unwrap it, which overload resolution prefers
  for a `Guid?`, so a request's `Guid?` and the `Guid` name one conversation, and one without a value throws as a null
  id does. The generated members take no ids: a run uses the host's, a resume resumes the host's conversation, and the
  settings members of a host with a settings id load, save and update the id's settings in place of the agent's own
  (D66). `WithStore<T>()` registers a store as each store interface its declared type implements, of any id type, and
  TSH028 reports only a type that is no store. Without any store, D69 keeps one store in memory per id type, and warns
  on the first write that one of them takes. A copy runs in its host's scope and disposes it as the host would (D76), so
  `await using var chat = runtime.CreateHost<T>().WithConversationId(id);`, the line the API invites, ends the unit of
  work it began. A scope's disposal is idempotent, so the host and its copies may each be disposed, and once one of them
  is, a run of any of them throws `ObjectDisposedException`, as the owner chose.
- **The raw path:** `ResolveOptionsAsync()` hands the host's conversation on with the options. On a host without one,
  `ChatOptions.ConversationId`, a string, names a conversation in the `IConversationStore<string>`, the owner's
  choice: the raw path has no type of its own to parse it into. On a host with one, a call's own options may name it
  by its text, as an Agent Framework session names it by the id the response gave, and naming another conversation
  throws an `ArgumentException` rather than storing the run apart from the host's (D26).
- **The tracker:** `ConversationTracker<TId>` lists, watches and steers the conversations under ids of `TId`, and the
  container has one for each id type an app asks for, as the owner chose ("Inject `ConversationTracker<int>` etc.; the
  container provides one per id type you ask for"). Beside registered stores, a tracker of an id type no
  `IConversationStore<TId>` keeps throws at its first call, with the message a run under such an id gets, since it
  would watch nothing forever. `AddCoreServices` registers the open generic, whose public
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
  whose `Runs` listed only the build's; registering the trackers of common id types up front, a list nobody chose;
  copies that own none of the scope, the first design, under which that line disposed a copy that owned nothing and
  leaked the scope, with every scoped service its runs resolved, without a word.

### D73. Every conversation can be kept {#d73}

- **Status:** owner.
- **Context:** the owner asked for an option, "if enabled by the user in the global settings", that "automatically
  generates conversation IDs when none was provided and stores them automatically", under a general configuration
  prefix, and accepted `WithOptions(options => options.PersistAllConversations = true)`.
- **Decision:** `TypeshiftOptions` holds what every run does, set by `WithOptions(configure)`, whose calls change one
  instance. With `PersistAllConversations`, a run of the host's own that has input, no conversation, no
  `ChatOptions.ConversationId` and no caller starts one: the conversation client gives a copy of the options a new id,
  so options the app reuses never share it. A run without input continues an interrupted run (D50), which a new
  conversation lacks, so it starts none. A raw-path call with input starts one whatever options it passes, since the
  client runs every call over the options it was created from (D26). New ids come from `NewConversationIds(create)`, typed like any id (D72), which concurrent runs
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
  call. That id is text, so `response.GetConversationId<TId>()` parses it as the store's id type, with the invariant
  culture, for `WithConversationId`: the text itself compiles there too and names a conversation under a `string`.
  A conversation's host loads its entries with `LoadConversationAsync()`, through the store of the id's type or
  D69's memory, which an app cannot resolve itself.
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

### D74. Live updates are on by default {#d74}

- **Status:** owner.
- **Context:** the owner asked to "ship with sane defaults, for stock users to have a working library at their hands
  from the get-go", and chose live updates on by default. `WatchAsync` threw until `WithRealtimeTracking()` was called.
- **Decision:** runs with a conversation publish their updates and `WatchAsync` reads them without a call.
  `TypeshiftOptions.LiveUpdates`, set through `WithOptions`, only tunes the interval and the limit, and refuses a
  negative interval and a limit below 1 where it is set. With no watcher an update costs a lock nobody else holds
  and a place in the list a watcher that joins late reads (D47), which the run's next stored step empties: nothing
  is cloned or merged. Merged text waits for the next update or the end of the model's stream, so no timer runs.
- **One place for options:** up to 0.1.0-alpha.6, `WithRealtimeTracking(options)` replaced a
  `RealtimeTrackingOptions` record, and without an argument only reset the defaults, since the updates are on anyway.
  It was a third shape of options beside `WithOptions(Action<TypeshiftOptions>)` and `WithFileStore`'s action, and
  the feature went by "realtime tracking", "live" and "watch". The owner chose to fold it into `WithOptions` as
  `LiveUpdates`, the guide's name for the feature, so every option of the app's runs is set in one call.
- **Where:** `Generated/ModuleRegistration.cs`, `DependencyInjection/TypeshiftOptions.cs`,
  `Conversations/ConversationTracker.cs`.
- **Alternatives:** keeping it opt-in, which the owner turned down; a switch to turn it off, which nothing needs yet;
  an `[Obsolete]` `WithRealtimeTracking` that sets `LiveUpdates`, which the release that breaks the rest did not need.

### D75. An app without a container creates a TypeshiftRuntime {#d75}

- **Status:** owner.
- **Context:** the owner asked for less ceremony, "Wolverine-style", and chose `TypeshiftRuntime.Create`, first named
  `TypeshiftApp`, over a factory on each agent. A first run took a `ServiceCollection`, `AddTypeshift()`, a provider,
  a scope and a host from it.
- **Decision:** `TypeshiftRuntime.Create(client, configure)` builds a container and returns its runtime, which owns it,
  and `runtime.CreateHost<TAgent>()` returns a host in a scope of its own (D76). The generator intercepts `Create` as it
  does `AddTypeshift()` (D1): the interceptor builds a `ServiceCollection`, registers the core services and the modules
  with its own `AddTypeshift`, and `ModuleRegistration.CreateRuntime` registers the client as an instance for every
  agent, the default client (D63), runs `configure` on the chain and builds the provider with scope validation. The
  container never disposes an instance, so the client outlives the runtime. The generator reads only a `Create` whose
  receiver names `TypeshiftRuntime` and skips a call bound at run time, such as one with a `dynamic` argument; a call it
  did not intercept throws, saying how to call it, and TSH040 reports each of them at build time: `using static`, an
  alias, a method group and a `dynamic` argument. The generator's pass binds the call to its sole candidate when the
  `configure` lambda names code it has not written yet. The library references
  `Microsoft.Extensions.DependencyInjection`, the container, which it did not before.
- **The chain's services:** `TypeshiftBuilder.Services` is the service collection the chain registers into, public
  as on the builders of Microsoft.Extensions, at the owner's choice. An app on `Create` registers its logging there,
  so a tool's exception, a failed save and the warning of D69 have somewhere to go, and the services its tools
  resolve (D15). A package writes a `With...` call of its own over it, for which `WithStore(create)`, the factory
  `WithFileStore` registers through, is public too.
- **Where:** `Hosting/TypeshiftRuntime.cs`, `Generated/ModuleRegistration.cs`, `Parsing/InterceptParser.cs`,
  `Emitting/InterceptorEmitter.cs`, `DependencyInjection/TypeshiftBuilder.cs`.
- **Alternatives:** a generated `Create` on each agent, which the owner turned down, and whose agents would not share
  a store or a tracker; a library `Create` that registers nothing, since only the generator knows the project's
  modules; a container of the library's own, which would serve neither scoped services nor the app's;
  `AddChatClient(client)`, whose factory makes the container own the client and dispose it with the runtime; a
  private service collection, with `services.AddTypeshift()` for an app that needs services of its own, which left
  a runtime from `Create` without a logger and its tools without services; `WithLoggerFactory(factory)` alone, which
  leaves the tools' services out.

### D76. The runtime is the root, and its hosts are units of work {#d76}

- **Status:** owner.
- **Context:** D75's first design gave the app one scope for its lifetime, so every run shared its scoped services,
  and an app with a container still built a scope by hand for each background job. The owner asked for one root in
  both kinds of app, after Wolverine's `WolverineRuntime`, which creates short-lived `IMessageBus` instances, and
  renamed `TypeshiftApp` to `TypeshiftRuntime`.
- **Decision:** `AddCoreServices` registers `TypeshiftRuntime` as a singleton over the root provider, so a singleton of
  the app, such as a background service, injects it. `Create` resolves that singleton from the container it builds and
  hands it the container. Disposing that runtime disposes the container, which disposes the runtime again, and the
  second call finds nothing left to dispose. A runtime your app's container created owns nothing, and disposing it does
  nothing. `runtime.CreateHost<TAgent>()` creates an async scope and a host that owns it: one unit of work, whose runs
  use that scope's services as D25 has every host use its scope's. `AgentHost<TAgent>` is `IAsyncDisposable` and
  `IDisposable`. Disposing a host the runtime created disposes its scope; disposing one the container resolved, such as
  one injected into a request, does nothing, since the container owns that scope. A copy from `WithSettingsId` or
  `WithConversationId` (D72) disposes what its host would: the unit of work is the host and its copies. A scope keeps
  every disposable the container builds until it ends, so the library builds the hosts of sub-agents and handoffs itself
  (`AgentHost<TAgent>.In`) rather than resolving them, or a long-lived host would collect one for each run.
  `runtime.Services` is the root provider, for singletons such as a `ConversationTracker<TId>`. A request still injects
  `AgentHost<TAgent>` from its own scope. Your app lets a host's runs and streams finish before it disposes the host,
  and a run that ends awaits the background sub-agents it stopped (D33), so none works on in a disposed scope.
- **The name:** `CreateHost<TAgent>()` was `Agent<TAgent>()` up to 0.1.0-alpha.6. A noun beside `AgentHost.Agent`,
  so `runtime.Agent<T>().Agent` compiled, hid that the caller owns what the call returns and must dispose it; a
  factory named for the disposable it creates says so, as the owner chose. It went without an `[Obsolete]` twin,
  since the release that renames it breaks other calls of the same code too.
- **The scope a job fills:** `host.Services` is the scope a host's runs use. A job has no request middleware to
  fill your app's scoped state, such as the current tenant, so it sets it there before it runs; without it, the
  tools of a job ran with that state empty, and the job fell back to a scope of its own made by hand. No lambda and
  no second object: the host is the unit of work, so its scope is a member of it.
- **Where:** `Hosting/TypeshiftRuntime.cs`, `Hosting/AgentHost.cs`, `Agents/Agent.cs`,
  `Generated/ModuleRegistration.cs`, `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** a separate `AgentScope` object that holds a scope and hands out hosts, which the owner turned
  down; one scope for the runtime's lifetime, D75's first design, whose scoped services, such as the chat client
  provider and what tools resolve, every run shared, concurrent ones included.

### D83. The runtime lists its agents and toolsets and hosts an agent by its kind {#d83}

- **Status:** owner, under the standing rule that a choice in line with the library's aim, typed agents and tools
  nothing has to be kept in step with by hand, needs no separate answer. Of the two options for
  `Toolset.DisplayName`, which nothing read, the recommended one: complete the tool picker the docs promised, rather
  than remove the member.
- **Context:** an endpoint for several agents (`/chat/{agent}`), an agent picked per tenant, and an admin page that
  lists agents and tools each needed a switch with one arm per agent type. Modules registered each agent and
  toolset as its own type only, so `GetServices<Agent>()` was empty, no host could be had for a `Type` or a name,
  and `WithDisplayName` named a toolset for a picker that had no way to list toolsets.
- **Decision:** `runtime.Agents` lists every agent of the app's modules as an `AgentInfo`: its type, name, kind and
  description. `runtime.Toolsets` lists every toolset as a `ToolsetInfo`: its type, display name and tools, each a
  `ToolInfo` with its name, description and the marks `ReadOnly()` and `RequiresApproval()`. `runtime.CreateHost(kind)`
  and `runtime.CreateHost(type)` create a host in a scope of its own, as `CreateHost<TAgent>()` does, typed as the
  public `IAgentHost`, which holds the host's members that take none of the agent's own `Settings`; a scope resolves
  the same host as a keyed `IAgentHost` service under the agent's kind. A host is looked up by kind, since names may
  repeat (D22) and the kind may not (D39). The generated `AddAgent` passes the agent's kind and `AddToolset` the
  tools the generator read, and both register a descriptor whose delegates are closed over the agent's or toolset's
  type in the generated call, so no type is made at run time and Native AOT holds. Names, descriptions and display
  names come from the instances their constructors created, read once, the first time the runtime lists them.
- **`IAgentHost`:** the interface the library already had for a sub-agent or an agent handed off to (D33, D34)
  became public, with the handovers kept internal, so only the library implements it. Its copies from
  `WithSettingsId` and `WithConversationId` are `IAgentHost`s too.
- **Where:** `Hosting/TypeshiftRuntime.cs`, `Hosting/RuntimeCatalog.cs`, `Hosting/IAgentHost.cs`,
  `Generated/ModuleRegistration.cs`, `Emitting/ModuleEmitter.cs`.
- **Alternatives:** a non-generic base class of `AgentHost<TAgent>`, which would move every member of the host into
  it; settings as JSON for code generic over the agent, which waits for a caller; listing the tools each agent
  offers, its own included, which needs the descriptions that stamps of referenced toolsets do not carry; removing
  `WithDisplayName` until a picker exists, which the guide's first toolset shows.

### D44. Settings records serialize themselves {#d44}

- **Status:** carried (v0 D27), extended; the strict read of a request came with the audit of November 2026, in the
  breaking changes the owner allowed before the next release.
- **Decision:** member by member through hidden helpers, no reflection; camelCase, tools under `"tools"` by name, nulls
  left out, the schema version as `"$schemaVersion"` above 1 (D40). The generator also writes a `JsonConverter` on the
  record, so `System.Text.Json` (an ASP.NET endpoint) writes the stored shape too. `ConversationEntry` carries the same
  converter, and a `TypeConverter` to `string` that Newtonsoft.Json writes the stored JSON through, so a store hands
  entries to either library as they are.
- **Stored documents are read leniently:** `Deserialize`, which every load from a store goes through, reads a
  member that does not read, missing or mistyped, as null, so it keeps the layer below. A member renamed or removed
  since the document was saved then costs its value and not every run of the settings; `WithSchemaVersion` and a
  migration bring the value back (D40).
- **A request's JSON is read strictly:** the `JsonConverter` reads the same way and then checks the JSON through a
  generated `CheckJson` per record. A member the record does not have, a value that does not read, an item or a
  dictionary's value that does not read or is null, and a key no member of its enum names throw a `JsonException`
  that names the member's path, which ASP.NET answers with 400. A JSON null passes, as missing, so a form can clear
  a field. Bound as leniently as a stored document, a PUT with PascalCase names, a number sent as a string or one
  misspelled member bound to empty settings, answered 200 and erased what the customer had stored. Code that reads
  stored JSON calls `Deserialize`, not `System.Text.Json`. `ConversationEntry` and the decisions take the interface's
  default `CheckJson`, which refuses nothing, since a store hands them to `System.Text.Json` as they are.
- **Alternatives:** a generated `TryDeserialize(json, out settings, out unread)` that an endpoint calls in place of
  binding, which leaves the endpoint the guide shows erasing settings until it changes; a warning in the docs alone;
  a strict flag through the readers, which would change the read every stored document goes through.
- **Details:** each overrides record writes and reads itself (`IJsonStorable<T>`), so an agent's `Settings` in
  another assembly than the toolset delegates to it. A document that is no JSON object throws, and so does one whose
  stored provider does not read (D77). Enums are stored by name, so reordering
  an enum keeps them. A value without a name, such as `[Flags]` with none set, a combination with a bit no member
  names, or a cast number, is stored as its number, in digits whatever the culture, and as those digits where it is
  a dictionary's key. A string of digits reads as the number too, which is how versions up to 0.1.0-alpha.6 wrote
  such a value before reading it back as absent. Non-finite numbers are stored as strings. The writer keeps text
  outside ASCII as it is (`JavaScriptEncoder.Create(UnicodeRanges.All)`), for settings, for conversation entries and
  for the file store's files: a default writer escaped every such character as `\uXXXX`, which doubled the bytes of
  Chinese, Japanese and Korean text and hid it from a database's search. Quotes, `<`, `>`, `&` and characters beyond
  the Basic Multilingual Plane stay escaped, since an API may pass stored JSON on to a page as it is; the relaxed
  encoder, which leaves them, lost for that reason. Readers take either form, so JSON stored before reads as it did.
- **Where:** `Emitting/StorageEmitter.cs`, `Generated/StorableJson.cs`, `Generated/IJsonStorable.cs`,
  `Generated/StorableJsonConverter.cs`.

### D78. Stored contents follow Microsoft.Extensions.AI's content contract {#d78}

- **Status:** from the audit of November 2026; in force until the owner decides otherwise.
- **Context:** an entry's messages are written and read through `AIJsonUtilities.DefaultOptions`, a closed
  polymorphic contract. A response with one content of a type outside it, such as a provider client's own, failed
  the append of its whole step, which D46 then logged and passed over, so the answer was missing from the
  conversation; a failed append now fails the run as it ends. One stored `$type` the installed version cannot read,
  after a package upgrade or downgrade, made every load of the conversation throw, for runs and for the app's own
  reads.
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

### D45. Tool JSON: contexts from the modules, reflection where the app keeps it {#d45}

- **Status:** approved.
- **Decision:** modules register the `JsonSerializerContext`s that list their tools' types (v0 D44). Where the app
  keeps JSON reflection, Microsoft.Extensions.AI's reflection-based defaults cover the rest, so tools need no
  context. Where it is off, a type no context lists is TSH027.
- **Where reflection is off:** the analyzer reads four build properties. `JsonSerializerIsReflectionEnabledByDefault`
  is `false`, which the SDK sets for an app with `PublishAot` or `PublishTrimmed`; or `PublishAot`, `IsAotCompatible`
  or `IsTrimmable` is `true`, the last two for a library such apps use. `PublishTrimmed` itself is not read: the
  Blazor WebAssembly SDK trims and sets the switch back to `true`, and an app may do the same, where an error would
  be wrong. Until 0.1.0-alpha.6 only the two AOT properties counted, so a trimmed app built and published without a
  word and its tools with a record or a list failed at run time.
- **Enums:** where JSON reflects, Microsoft.Extensions.AI's defaults carry a `JsonStringEnumConverter`, which wins
  over every context, so a tool's enum is its name in the schema, the arguments and the result. Where it does not,
  the defaults carry none, the context that lists the type decides, and its default is the number: the model reads
  `integer`, and a name it sends fails the call. TSH033 reports a context that lists a tool type with an enum
  and has no `UseStringEnumConverter = true`. It skips an enum, and a property, with a `[JsonConverter]` of its
  own, and an enum the context's `Converters` name a converter for, and it shares TSH027's condition. It was a
  warning while it was built, before 0.1.0-alpha.8, since the tool still works with numbers, and is an error at the owner's request: a
  model sends the names it knows, so the tool failed in the published app alone, after a build that passed. The
  generator registers no converters itself: that would need
  every enum of every tool and typed-run type found at build time, nested ones included, where one line on the
  context does it.
- **As built:** TSH027 knows the types Microsoft.Extensions.AI's own context lists (strings, numbers, `bool`, dates,
  `TimeSpan`, JSON elements) and the contexts of the compilation; a tool returning `IAsyncEnumerable<T>` returns a
  `List<T>`, which it checks. The console sample publishes as Native AOT in CI, where a trim or AOT warning fails the
  build, and a second time trimmed without AOT, through a `TrimmedOnly` switch in its project, since
  `-p:PublishTrimmed=true` on the command line reaches the generator's netstandard2.0 project (NETSDK1124).
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
- **Results reach the provider as text (owner):** a provider's client writes a result that is no string with
  Microsoft.Extensions.AI's defaults, which indent, and passes a string on as it is. Through the OpenAI and the
  Anthropic clients, a tool returning 20 records went out as 2,947 characters and 1,102 tokens against 2,106 and 682
  without white space, on every model call that carried it. `ToolJson` writes results without indentation, and the
  wrapper each run gets from `InstrumentedClients` hands the provider's client a result that is a JSON object or
  array as its text, written with the defaults' escaping so that only white space differs. Each result is written
  once per run, in a table keyed weakly by the content, which every later call of the run sends again. Everything
  above that wrapper keeps the `JsonElement`: middleware, the stored entries, the response, watchers and
  compaction's estimate. A client in the provider's place that cast a result to `JsonElement`, a test double above
  all, reads the text instead.
- **Content is no JSON:** a tool that returns an `AIContent`, a type derived from it or a sequence of them, as its
  value or awaited, hands the content on as it is, as `AIFunctionFactory` does, so the model receives an image as an
  image. The parser gives such a tool a return kind of its own and no result type, so TSH027 asks no metadata for
  it, and the generated function calls the hidden `GeneratedToolFunction.GetContentResult`, which reads a sequence
  once into a list, since an iterator would run again for each reader. Microsoft.Extensions.AI writes the content
  with its `$type` but reads a result back as a `JsonElement`, which a later run would send as text, so a stored
  entry marks such a result (`typeshift.content_result`) and reads it back as content; a result without the mark
  stays JSON, also one whose JSON looks like content. A stream of contents is still one JSON array.
- **Where:** `Analysis/TypeshiftAnalyzer.cs`, `Analysis/JsonEnums.cs`, `Parsing/Discovery.cs`,
  `TypeshiftGenerator.cs` (`CreateModule`), `Generated/GeneratedToolFunction.cs`, `Generated/ToolJson.cs`,
  `Telemetry/InstrumentedClients.cs`, `Emitting/ToolsetEmitter.cs`, `Parsing/ToolsetParser.cs` (`IsContent`),
  `Conversations/ConversationEntry.cs`, `samples/Typeshift.Samples.Console`.
- **Alternatives:** an option on `WithOptions` that keeps the element, one more thing to explain where every shipped
  client passes text on as it is; leaving the indentation and saying so in the guide.

### D62. Union types in tools and typed runs {#d62}

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
    A JSON null binds the empty union, also for a required parameter, where a null fails the call for every other
    type that takes none (D53): `GeneratedToolFunction.GetArgument` asks `default(T) is IUnion`.
  - Every schema `ToolJson` builds copies a union's type onto each case that names none, and a nullable parameter
    widens each case as well (D48). Polymorphic types get the same.
  - A typed run builds its answer's schema as a tool's parameters are built, XML docs included, and holds anything
    but an object with properties under `data`, as Microsoft.Extensions.AI holds a number. A contract of its own for
    `T` reads the answer from under `data`, since a converter in the options loses to an enum's converter and to a
    polymorphic type's metadata.
  - A schema node the serializer writes for a type it cannot handle, `"Unsupported .NET type"` with `not: true`,
    throws a `NotSupportedException` that names the member, since a provider rejects the node far from its cause, as
    the owner's `Exception` member showed.
- **Where:** `Generated/UnionCaseClassifier.cs`, `Generated/ToolJson.cs`, `Generated/ToolSchemas.cs`,
  `Hosting/ResultFormats.cs`, `Hosting/AgentHost.cs`.
- **Alternatives:** a `[JsonUnion(TypeClassifier = ...)]` on every union, code each app writes; strict numbers
  everywhere, which fails a model that sends `"5"` for a number; leaving the typed run's schema to
  Microsoft.Extensions.AI, which neither types the cases nor holds an object union under `data`.

### D64. A typed run can stream {#d64}

- **Status:** owner.
- **Context:** the owner asked for a strongly typed answer from a streaming run.
- **Decision:** `RunStreamingAsync<TResult>` asks for the same response format as `RunAsync<TResult>` (D62) and
  returns a `StreamingRun<TResult>`. It streams the updates as `RunStreamingAsync` does and, once they are read to
  the end, holds a `ChatResponse<TResult>` in `Response`. It calls the model when it is read and only once, since
  reading it again would run the agent again. Every other stream of the host, untyped, an answer's or a resume's,
  is a `StreamingRun`, the base of the typed one, read once and holding its `ChatResponse` in `Response`: an
  untyped stream read twice ran the agent twice up to 0.1.0-alpha.6, the input stored twice, and a caller had no
  response to read the decisions from. `ResumeAsync<TResult>` and `ResumeStreamingAsync<TResult>` resume a typed
  run typed. Both typed runs take a `ChatMessage` as well, so the run that answers
  an approval asks for the format again. The format is the run's, not the agent's: an agent handed the run off to, or
  answering in a handed-off conversation, answers in it, and a sub-agent answers in its own.
- **Where:** `Hosting/StreamingRun.cs`, `Hosting/AgentHost.cs`, `Hosting/ResolvedChatOptions.cs`,
  `Hosting/IAgentHost.cs` (the format in `RunHandover`), `Conversations/ConversationChatClient.cs`,
  `Generated/AgentRuns.cs`, `Emitting/AgentEmitter.cs`.
- **Alternatives:** partial objects parsed from the JSON as it grows, which needs a parser that reads incomplete JSON
  and gives up the types System.Text.Json checks; a list of updates the caller collects and a helper that reads the
  result from them, two calls where one serves.

## Conversations

### D46. Tracking, carried and fixed {#d46}

- **Status:** carried (v0 D18), with the audit's fixes.
- **Decision:** tracking wraps the tool loop and marks where a run begins and ends; checkpoints inside it store
  each step (D50). The provider never receives the
  conversation id. On resume, each result follows the message that made its call, and unanswered calls, results
  without their call and error messages are left out, and so are the approval requests and answers a version before
  D80 stored. A failed save is logged and traced, and the run goes on, storing what it can, to its end; then it
  throws a `ConversationStoreException` that holds what it answered, with the store's exception inside. The
  conversation is the record approvals, the mode and resumes rely on, so a run that could not write it is no
  success: a lost end makes a finished run look interrupted and resumable, a lost answer is gone at the next load.
  `TypeshiftOptions.StoreFailure = StoreFailure.ContinueRun` lets an app have such a run return, with
  `ConversationIncomplete` set on the response (and on a stream's last update), and the run's span is an error
  either way. A `StoreConflictException` from an append is no failed save: a run of another container appended
  under the entry's number, and the run stops as busy (D50). The response that asks your app and its decision must
  be stored whatever the option says, since a decision without its call could not be carried out (D81): that run
  throws a `ConversationStoreException` at once, with the store's exception inside and the model's response that
  asked in `Response`, so every run whose storing failed throws the same type (owner, for 0.1.0-alpha.8). The
  agent's name goes on `AuthorName`, never into `AdditionalProperties`.
- **Details:** the end of a run is an entry of its own (`EndsRun`), with an `ErrorContent` when the run failed, was
  cancelled or its stream was left early: the exception's message, streaming or not, and a fixed text for a stream
  its caller left, which no exception explains. Calls and results pair by call id, which D59 keeps unique in a conversation;
  of a call or a result a store holds twice, the last copy counts. The checkpoints know what they stored by reference, since the tool loop
  hands the same messages on; a streamed response is stored from the updates, and the tool loop's own messages
  built from those updates are matched by count.
- **Where:** `Conversations/ConversationChatClient.cs`, `Conversations/CheckpointChatClient.cs`,
  `Conversations/ConversationHistory.cs`, `Conversations/ConversationStoreException.cs`.
- **Alternatives:** returning the answer as before and only logging, which hands an app a chat that answered and
  forgot behind a success, and where a container-less app had no log at all; returning the answer with a mark and
  nothing more, which leaves the trap open for every app that never reads the mark; failing the run at the first
  step that was not stored, which stops a run whose tools may have acted already and leaves the app without the
  answer it paid for. A failed save was not a failed run until the owner chose loud failures over quiet ones.

### D47. Live updates are a feed per conversation {#d47}

- **Status:** approved.
- **Decision:** runs publish their updates from the start (D74), and `TypeshiftOptions.LiveUpdates` tunes them;
  `ConversationTracker.WatchAsync(conversation)` reads a bounded feed that drops its oldest updates when the watcher falls behind. A run never waits
  for a watcher. `PublishInterval` (100 ms) and `MaxPendingUpdates` (256) are options. `Runs` lists the runs of
  conversations going on in this container.
- **Alternatives:** v0's process-wide events, awaited up to 5 s per model call.
- **Details:** merged text waits for the next update or the end of the model's stream, so no timer runs per
  watcher, and an update of another message, response, role or author starts a merge of its own, as
  `ToChatResponse` tells messages apart. A run that does not stream publishes each response and tool result as it
  stores them, through the same publisher; a streaming run publishes each update it hands its caller, the results
  the run writes itself, such as a handoff's, included.
- **A watcher that joins late:** the run keeps what it published since the store last took a step of it, and a
  watcher that subscribes while the run goes on reads that first, merged into one update per message, then what
  follows. A page that loads the stored conversation and then watches sees an answer in flight from its start; the
  model's response reached the store only once its call ended. The watchers' feeds, the run's publisher and that
  list share one lock per conversation (`LiveConversation`), so a watcher that joins between two updates reads each
  once: in what it reads first or in its feed. Merged text keeps the finish reason of the update that ends it.
  Before, a watcher that joined while the model wrote read the answer from the middle.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/LivePublisher.cs`.
- **Alternatives, for the late watcher:** a replay of the whole run, which the store holds already; keeping the
  updates only while someone watches, which leaves nothing for the one who joins.

### D50. A conversation is stored step by step and resumes after a crash {#d50}

- **Status:** approved; one run across containers by the entries' numbers: owner, with the audit of November 2026;
  every store refuses a taken number: owner, for 0.1.0-alpha.8.
- **Context:** the first draft stored a run when it ended, so a run that died after an hour of work lost all of it.
  Later, one run per conversation held in a container only, and two instances of an app over one store, the usual
  deployment behind a load balancer, ran one conversation at once: their entries interleaved, and before D80 a
  retried answer ran its call in each. The owner chose "providing a sequence/version to the store method", for "two
  instances of the same application without immediate synchronization of a single conversation".
- **Decision:** a conversation is a log of `ConversationEntry`s: a run's input, each model response (with model,
  provider and usage), each tool result, compaction summaries; the run's last entry has `EndsRun`. Checkpoints append
  each step the moment it exists, tool results before the next model call. A run without an ending entry was
  interrupted: `ResumeAsync()` (and `ResumeStreamingAsync`) on the conversation's host continues it without new
  input, leaving out a call that was in flight; a new run on the conversation also starts from its steps. A
  conversation whose last entry holds the model's answer, with no call and nothing after it, lost only its end:
  resuming it stores the end and returns that answer, since a model called again would answer a second time.
  Resuming also carries out a batch of decisions your app answered, and while decisions are still open it calls no
  model and returns them (D80, D81). Resuming a conversation whose last run ended and that waits for nothing throws
  a `NothingToResumeException`.
- **Retrying a run that failed:** a run that fails at the model call has stored its input, and its end holds the
  error (D46), which the model never reads, so running again with the input put it into the conversation twice.
  `RetryAsync` and its streaming and typed twins continue what a resume continues, and also a conversation whose
  last ending entry holds an `ErrorContent`: a failed, cancelled or abandoned run. The run takes no input and
  stores none, the agent that answers in this one's place retries too, and a conversation whose last run answered
  or waits throws a `NothingToResumeException` with a message of its own. `ResumeAsync` keeps refusing an ended
  run, since a retry is a choice the app makes, after an error it saw, and a resume one it makes after a crash. A
  model fallback (choosing another provider for the retry) is not built, and a retry does not need one: settings
  for the retry may name another model.
- **One run at a time:** one run per conversation at a time in a container, a service provider, an app's or
  that of a runtime from `TypeshiftRuntime.Create`, so two runtimes in one process are two. A run claims its
  conversation there before it loads it, so no run reads it while another still stores its end. A run that finds
  the conversation claimed by a run still loading it waits until that run began or let go, and one that finds a run
  ending, which takes no steering input any more (D51), waits until it ended or handed the conversation on. One that
  finds a run going on that takes steering input throws a `ConversationBusyException`, since that run takes the
  input. Across containers, the store refuses a second run's entries by their numbers (details). Both exceptions
  derive from `InvalidOperationException`, so an endpoint tells a busy chat from a mistake by type and a `catch`
  written before them keeps working.
- **Details:** across containers, each entry carries a `Sequence`. A run numbers its first entry one past the
  highest number it loaded, an entry stored before entries had numbers counting one past the entry before it, and
  takes the next number for each append it tries, so a failed append leaves a gap and never a number used twice.
  Every store refuses an entry whose number its conversation holds, as a unique key on the conversation and the
  number does, by throwing `StoreConflictException`, and `VerifyAsync` checks it (D58). The run that appended second
  then stops with a `ConversationBusyException` that holds the store's exception, before its next model call or tool
  call; it stores nothing more and keeps what it stored. Busy fits what its caller can do: the other run holds the conversation, so
  the caller starts again once that run ended, as for a run busy in its container, and a 409 handler that exists
  for one serves both. The failed-save path of D46 would let the run go on, interleaving, or return under
  `StoreFailure.ContinueRun`. A refusal of what a run stores as it ends, its end or the input no model received,
  ends nothing more: the run answered already, and the other run's entries follow it. Older entries have no number
  and load as 0. A run that loads from a summary without a number, with no number after it, numbers from the whole
  conversation, once, since numbered
  entries may come before that summary when an earlier version of the app wrote it during a deploy or after a
  rollback. The numbers and the decisions document (D80) work together: the document makes answering and starting
  a batch happen once, with or without numbers, and the numbers keep the history in order around it. A batch one
  container carries out and another finds started and repairs (D81) gets the results of whichever appends first,
  and the other run stops.
- **Alternatives:** one entry per run, rewritten at each step (a whole-run write per model call); re-running the
  interrupted tool call, unsafe for side effects; a `RunAsync` with a null input in place of `ResumeAsync`; a
  `ResumeAsync` that continues a failed run too, where a test pins the refusal; for the
  run that finds the conversation still loading, a claim that queues steering input for the run it becomes, which
  makes `TrySteer` true earlier and is open for later; one host call that steers or runs, since two calls are never
  atomic for the app, which is API of its own. Across containers: leaving one run to the app (up to 0.1.0-alpha.6),
  which gave it nothing to keep runs apart with short of a lock around every run, streaming included;
  `AppendConversationAsync(conversation, entry, expectedLast)`, as a save takes a version, which breaks every store;
  a lease or a heartbeat in the store, a method every store implements, open for steering and live updates across
  containers; refusing as an option, which a store declared with `RefusesTakenSequences` (tagged 0.1.0-alpha.7, never
  published), where a
  store without the key interleaved two instances' runs without a word; a refused run that loads again and goes on,
  which interleaves with a run still going on; numbering
  from the last loaded entry instead of the highest, where a number skipped after a failed append and taken by
  another run left every later run of the conversation refused.
- **Where:** `Conversations/`, the checkpoint layer of D29, `Conversations/ConversationRun.cs` (the numbers and the
  refusal), `Conversations/ConversationChatClient.cs` (where a run's numbers start), `Storage/IConversationStore.cs`.

### D51. Steering a run going on {#d51}

- **Status:** owner.
- **Decision:** `ConversationTracker.TrySteer(conversation, input)` hands input to the run of the conversation going
  on in this process; the checkpoint layer injects it before the run's next model call and stores it, once run
  middleware let it through (D30), and the run does not end while such input waits. It returns false when no run of
  the conversation is going on in this process, while the run still loads the conversation, or once the run has
  decided to end, and the host starts a run with the input instead: that run waits for a run that is ending (D50),
  and a run that began in between refuses it with a `ConversationBusyException`, after which the host steers again.
  Input a run took and that no model received because the run failed, was cancelled or spent its limit is stored
  with its end, so `TrySteer`'s true promises the input reaches a model, of this run or the next. Stopping a run is
  cancelling its token. Steering input goes in after the message the tool loop's messages ended with when it
  arrived, found by reference in every later call of the run, so context that grows between passes cannot move it
  between a call and its result. The call's contents are read once for all the inputs, which go back in their
  order, rather than searched for each input, which cost seconds a call once thousands of inputs waited. Input a run took and no model received is stored with the run's end or its
  handoff. Steering input a run takes after it asked your app expires what it asked, as a new message does (D81). A
  background agent's answer expires nothing and starts no model call while a batch is open: it is stored, and the
  run that carries the answers out reads it. A run holds at most 32 of the user's inputs, or 262,144 characters of
  their text, that no model received yet, and `TrySteer` throws a `ConversationBusyException` for input beyond that:
  before, a client that steered faster than the model answered grew the next call and the conversation without end
  (2,000 inputs of 100 kB in one model call). Returning false instead would send the host's loop to start a run,
  which the run going on refuses, and round again. The bound is a constant rather than a member of
  `TypeshiftOptions`, until an app needs another. A background answer counts toward neither number, and the user's
  input may not carry its mark, since run middleware lets a marked message pass unseen.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/ConversationRun.cs`.
- **Alternatives:** a steering queue in the store, which works across processes but reads the store before every
  model call; queuing input for after the run, which is just the next run.

### D52. Plan and auto modes are built in {#d52}

- **Status:** owner.
- **Decision:** `AgentMode` `Default`, `Plan`, `Auto`, a run option (`WithMode`, configurable like any run option).
  `Plan` offers only tools marked `ReadOnly()` (toolset tools, MCP servers with `UseMcpServer(...).ReadOnly()`, or
  tools of the chat options added with `WithReadOnlyTools(...)`), and a built-in `exit_plan_mode(plan)`, which asks your app with a `PlanDecision`; `plan.Approve(mode)` continues in
  `Default` or `Auto`, a rejection's reason sends the model back to planning. Handoffs are not offered in plan mode:
  an agent that plans answers itself until its plan is approved. Offering them is open, now that the agent handed
  to would plan in the conversation's mode. Outside a host's run in plan mode a
  call of `exit_plan_mode` asks nobody: the model reads that it is not planning, so no model can raise a plan prompt
  in a conversation that never planned (D81). `Auto` runs tools that require approval unless a
  rule rejects the call. A conversation keeps its mode once something names one: a run whose settings name a mode,
  an approved plan, or `SetModeAsync` on the host. From then on every run of the conversation is in it, whichever
  agent answers (D34), until one of the three names another; an approved plan into `Auto` therefore holds until
  your app switches back, which `SetModeAsync` does without a configurable `Mode`. While nothing named a mode, each
  run is in its agent's own. Plan mode appends a short instruction to research, then call `exit_plan_mode`.
  Sub-agents run in their caller's mode; plan mode offers them, and in it a sub-agent gets its read-only tools and
  no `exit_plan_mode`, and answers its caller.
- **A configurable mode:** `Configurable(r => r.Mode)` makes the mode a member of the agent's `Settings`, so
  settings that are stored per customer or posted by a page can name `Auto`. The owner kept the mode configurable
  and chose a warning: TSH037 reports the pick on an agent that offers a tool or an MCP server that requires
  approval. A rule's rejection holds in every mode, which is the fix the message names beside removing the pick
  and switching a conversation's mode with `SetModeAsync`, which needs none.
- **As built:** the mode is a member of the conversation's decisions document (D80), null until named. A run whose
  settings name a mode saves it when it takes the conversation, before it learns which agent answers, and the save
  that starts an answered batch switches to an approved plan's mode. A run reads the document when it begins and
  stays in that mode to its end. Entries no longer store the mode. One an earlier version stored on a run's ending
  entry or on a summary holds while the document names none: the run that finds it copies it into the document with
  its first save, and until then repeats it on a summary it writes, since later runs read nothing before a summary.
- **Tools of no toolset:** `WithReadOnlyTools(...)` records the tools on the agent by reference, and plan mode offers
  a tool it recorded. A provider's client knows a hosted tool, such as `HostedWebSearchTool`, by its type, so the mark
  wraps nothing: the provider receives the tool itself. A context provider's tools carry no mark and stay hidden.
- **Where:** `Agents/RunOptions.cs`, `Decisions/ExitPlanModeFunction.cs`, `Approval/ModeChatClient.cs`, the gate of
  D81, `Decisions/DecisionLedger.cs` (`SetModeAsync`, `StartAsync`), `Conversations/ConversationChatClient.cs`,
  `Agents/ChatOptionsBuilder.cs` (`WithReadOnlyTools`).
- **Alternatives:** modes as tools the model calls (MAF's `mode_get`/`mode_set`), which lets the model leave plan
  mode unasked; a classifier built into auto mode, where a rule the host writes can ask any model it chooses; the
  mode on each run's ending entry, which every agent of a handoff had to pass on and every summary to repeat, and
  which a run's settings were the only way to change; an approved plan's mode holding for one run only, and a mode
  that is never a stored setting, both left open; a wrapper that marks a tool of no toolset as reading only, which
  hides a hosted tool from the client that sends it.

### D54. Runs starting and ending are a feed too {#d54}

- **Status:** owner.
- **Context:** a list of running conversations, or a dot on each, had to poll `Runs` every second.
- **Decision:** `ConversationTracker.WatchRunsAsync(ct)` yields the runs going on, then each run as it starts and as
  it ends (`LiveRun.Ended`), from a bounded feed per watcher like D47's, sized by `MaxPendingUpdates`. It works
  without a setting, as `Runs` does.
- **As built:** the watch subscribes before it lists the runs going on, so a run starting at that moment is listed or
  fed, and its start is not reported twice.
- **The end in the conversation's feed:** `WatchAsync` loops across runs, so a watcher that waited for one answer had
  no update to stop at. A run's last update to the conversation's watchers has `update.EndsRun` set, after its
  other updates and before a run that waits for the conversation begins. It holds the finish reason of the run's
  last model response, or, for a run that failed or stopped, the error its stored end holds as an `ErrorContent`.
  A handoff publishes no end for the agent that handed on, so the conversation's watchers hear one end, after the
  answer of the agent handed to. The run's own caller gets no such update: its stream ends.
- **Where:** `Conversations/LiveRuns.cs`, `Conversations/ConversationTracker.cs`.
- **Alternatives:** C# events, where a slow or throwing handler runs inside the run; a separate change record, which
  needs one more id-type alias.

### D59. A call id the conversation holds already is renamed {#d59}

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

### D60. An answer to a closed request throws; steering that the run answers closes its requests {#d60}

- **Status:** owner; superseded by D80, which stores whether a decision is open, and D81, which expires what waits.
- **Was:** a request was open while the conversation's last run left it without an answer, until the agent answered
  steering input stored after it. `OpenRequests` worked that out from the stored messages, and a run whose input
  answered a request that was not open threw `ApprovalClosedException`, naming each request with an
  `ApprovalClosedReason`: answered, passed by, or unknown.
- **Now:** whether a decision is open is the state of its batch in the conversation's decisions document. An answer
  to a decision that is not open throws `DecisionClosedException`, naming each with a `DecisionClosedReason`
  (`Answered`, `Expired` or `Unknown`), and records nothing (D80). New input, or steering input the run takes after
  it asked, expires the batch, and the model reads a result for each of its calls (D81).

### D80. Decisions are records in a document per conversation {#d80}

- **Status:** owner.
- **Context:** the owner asked for "a proper decision system ... like a proper user Q&A tool". What a run asked your
  app was Microsoft.Extensions.AI's approval contents inside the conversation's history, and whether a request was
  open, answered or carried out was worked out by scanning messages, in six files (D24, D60). Where two of those
  scans disagreed, an approved call ran twice, and calls ran after the user had sent something else.
- **Decision:** a decision is a stored record with an id and a state: an `ApprovalDecision` for a call of a tool that
  requires approval, a `PlanDecision` for `exit_plan_mode`, a `QuestionDecision` for `ask_user` (D82). A conversation
  has one decisions document (D79): at most one batch, the tools approved for the conversation, the conversation's
  mode (D52), and the ids of the last batch's decisions with why they closed. A batch holds every call of one model
  response of which at least one waits for your app: with an ask for
  a call that waits, with a rejection for a call a rule rejected, bare for a call that runs with the batch. It
  repeats each call's name and arguments, so the decisions are listed and the calls replayed from the document
  alone. An answer (`DecisionAnswer`) names its decision by id. The same answer again changes nothing; another
  answer, or one to a decision that is not open, throws `DecisionClosedException` and records nothing of its call;
  one that does not fit its decision throws `ArgumentException`. Answering is an API of its own: the generated
  `AnswerAsync(answers)` records the answers and, once no decision is left open, continues the run;
  `RecordAnswersAsync` records them without a run, `ResumeAsync` continues, `CancelDecisionsAsync` withdraws
  what waits, and `GetDecisionsAsync` lists what waits, with the tools approved for the conversation, each of which
  `RevokeStandingApprovalAsync` withdraws. No answer travels in a run's input (D81).
- **Details:** a batch is `open` until its last answer, then `answered`, then `started` once a run begins to carry
  the answers out, then gone and remembered as answered. `started` is the commit point: from it on every call the
  batch would run counts as started, so no later run starts one again. An open batch that new input passes by is
  `expired`, then gone and remembered as expired. A batch your app withdraws while it is open or answered is
  `cancelled`: an answered one too, since its answers were not carried out and a withdrawal that left approved
  calls to run would surprise. The conversation's next run stores a result for each of its calls, as for an
  expired one, and it is then gone and remembered as cancelled; a run without input goes on to the model, which
  reads the withdrawal. Standing approvals join the document when their batch starts,
  never when they are recorded, so an answer that expires grants nothing, and leave it when your app withdraws
  them. A run reads them, and the mode, when it begins, so a withdrawal reaches the conversation's next run: a
  check on every call would cost a load of the document for each. `DecisionLedger` is the only code that
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

### D81. The tool loop never pauses: a gate below it decides each call, and a replay carries the answers out {#d81}

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
  exists in the store: a save that fails, or a response whose append failed, fails the run at once with a
  `ConversationStoreException` around the store's exception (D46). A batch another container's run opened
  meanwhile is no failed save: that run throws a `ConversationBusyException`, as a refused append does (D50). A
  host's run without a conversation has nowhere to keep a decision, so it throws
  `DecisionUnavailableException` before its first model call when it offers a call nothing but your app could
  decide, naming the tools: a tool that requires approval while no `IToolApprovalRule` is registered and the run is
  not in auto mode, or `exit_plan_mode` in plan mode. The mode layer checks it, where the run's tools are complete.
  The owner's line is loud instead of silent: such a run failed at the first such call, after the model was paid
  and earlier calls ran, and passed every test in which the model never made it. With rules registered, any of them
  may approve, so the run starts, and a call no rule decided throws the same exception there. `ask_user` is not
  offered there (D82), as handoffs are not (D34), so an agent that declares questions runs without them and fails
  nothing. A streaming run holds back its updates from the first call of a response until the stream ends, since the loop
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
  bridge for clients that speak that protocol, if one is built. For a run without a conversation: throwing only at
  the call (up to 0.1.0-alpha.6), after the model was paid and earlier tools ran; rejecting the call with a reason
  the model reads, as a sub-agent's is, which hides a setup that cannot work behind answers that go on.

### D82. Questions to the user are a built-in tool an agent declares {#d82}

- **Status:** owner.
- **Context:** the owner asked for the decision system to cover "user input, kind of like the Claude Q&A tool
  works": the model asks, the run waits, and the user's answer goes back to the model. An approval and a plan
  already wait as decisions (D80), below a tool loop that never pauses (D81).
- **Decision:** `UseQuestions()` in an agent's constructor offers the model a built-in tool, `ask_user`. It takes
  one to four questions, each with a question text, a short header, two to four options with a label and a
  description, and an optional `multiSelect`. A call of it is a `QuestionDecision`, asked and answered like every
  decision: `question.Answer(...)` takes one `QuestionAnswer` for each question, the labels the user picked, their
  own words, or both, and `Reject(reason)` declines. The model reads the answers as the call's result, as JSON with
  each answer under its question's header, or that the user declined. Arguments that break the tool's shape open no
  decision: the gate answers the call with what is wrong, and the model asks again.
- **Details:** the tool is offered where a decision can wait and the agent declares it: in a host's run with a
  conversation, in every mode, plan mode included. A sub-agent is not offered it (D33; TSH034 warns at the use), nor
  is a run without a conversation, and a call of it there is answered as not available, which fails no run. Auto mode
  answers no question, and neither do approval rules or standing approvals, which concern tools that require
  approval. An answer is checked against the stored questions when it is recorded: another number of answers, a
  label the question does not offer, several labels for a question that takes one, or an empty answer throws
  `ArgumentException`. A string converts to an answer and picks the option whose label it equals. `QuestionAnswer`
  and `DecisionAnswer` compare their lists by value, so the same answers recorded again change nothing (D80). The
  function itself never runs: the loop's invoker answers its call from the record. `UseQuestions()` is recorded on
  the agent at run time, as `UseAgent` and `UseHandoff` are, so the generator writes no code for it, only the module
  stamp's `AsksUser`, which the analyzer reads for an agent of a reference.
- **Where:** `Decisions/AskUserFunction.cs` (the schema, the check and the result), `Decisions/Question.cs`,
  `Decisions/DecisionChatClient.cs`, `Approval/ModeChatClient.cs` (where it is offered), `Agents/Agent.cs`,
  `Parsing/AgentParser.cs`, `Analysis/TypeshiftAnalyzer.cs`.
- **Alternatives:** a tool of the app's own that blocks until the user answers, which holds a run open across
  requests and loses it at a restart; free-text questions without options, which leave a UI nothing to render but
  a text box, where options with a free answer serve both; an `IQuestionHandler` callback, a second way to answer
  beside `AnswerAsync`; asking through a sub-agent, which needs the child's run stored (D33); failing a run without
  a conversation of an agent that declares questions before its first model call, as one that offers a tool that
  requires approval fails (D81), which turns a chat agent that a job or a one-shot call reuses into one that only
  runs in a conversation, while a question, unlike such a tool, is never offered there.

## Repository

### D48. Carried as they were {#d48}

- Sealing (v0 D19), CS1591 off in tests only (v0 D20), samples use a scripted client (v0 D21), package metadata
  (v0 D22), releases by tag through trusted publishing (v0 D37), the Typeshift name (v0 D38), the package README
  (v0 D39), nullable parameters widened at the schema root (v0 D12).
- NuGet imports a package's build files only under the package's ID, from the folder of the project's framework, so
  the props and the targets are packed as `buildTransitive/net11.0/Typeshift.props` and `Typeshift.targets`. Outside
  a framework folder they would count as an asset for every framework, and a project on .NET 10 would restore the
  package (D61).

### D49. Diagnostics are numbered afresh {#d49}

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
