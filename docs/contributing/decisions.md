# Decisions

Why the code is shaped the way it is. Each entry gives the context, the decision, the alternatives that lost and where
the decision lives in the code. IDs are stable: code comments cite them (`// D25: ...`), and a decision that no longer
holds moves to the [withdrawn](#withdrawn) table, pointing to what replaced it. "Spec" means the alpha's design
document; "owner" means the maintainer, whose review shaped D25 to D36.

The ones that shape the API the most: D25 to D27 (one store of self-serializing documents, the generated settings
records), D29 and D30 (generated tool lists and `Configure{Tool}()`, so misuse does not compile), D40 to D42 (settings
as a tool parameter, one flat chain per declaration), D33 (conversation ids parsed into the store's ids), D1 and D4
(the intercepted `AddTypeshift()`, the typed runtime members) and D44 (JSON contexts registered by the modules).

Before changing code an entry names, read the entry. To change the decision itself, edit the entry in the same
commit, or add a new one and withdraw the old.

## Registration and the generator

### D1. `AddTypeshift()` exists only as an intercepted call

- **Context:** a generated `AddTypeshift()` per assembly is ambiguous (CS0121) wherever `InternalsVisibleTo` exposes
  another assembly's internals, and every test project does.
- **Decision:** the library ships one public `AddTypeshift()` that throws an `InvalidOperationException` saying what to
  check; the generator intercepts every call in the project that makes it. The method lives in the
  `Microsoft.Extensions.DependencyInjection` namespace, like every `Add...` method, so hosts need no extra using.
- **Alternatives:** a generated method with a per-assembly name (`AddTypeshiftForMyApp()`), which leaks the assembly
  name into every host; module initializers, which run on load and cannot see the service collection.
- **Where:** `DependencyInjection/TypeshiftServiceCollectionExtensions.cs`, `Emitting/InterceptorEmitter.cs`,
  `buildTransitive/Typeshift.props`.

### D2. The generated names

- **Decision:**
  - per toolset: `{Toolset}.Tools.{Tool}`, a `ToolRef` or `ToolRef<TSettings, TOverrides>`; per tool with
    configurable settings a nested `{Tool}Overrides` record; a `{Toolset}ToolList` class whose extension block adds
    one property per tool to `ToolList<TToolset>`, named like the tool's property in `Tools`;
  - per agent: the nested `Settings` record; per tool with settings a private `Configure{Tool}()` returning a nested
    `{Tool}SettingsBuilder`; a `{Agent}RuntimeExtensions` class with the typed members of `AgentRuntime<TAgent>`; a
    nested agent's class name joins its containers' names, so twins in two classes do not clash;
  - per assembly: `Typeshift.Generated.{Assembly}Module`, with every character of the assembly name that is not a letter
    or digit replaced by `_` (`Typeshift.Samples.Console` gives `Typeshift_Samples_ConsoleModule`);
  - the interceptor: a `file static class AddTypeshiftInterceptor` in `Typeshift.Generated`;
  - a generated member named like a member next to it gets a suffix: a tool `equals` is `Tools.EqualsTool` and
    `t => t.EqualsTool`, since `Equals` is an inherited member.
- **Alternatives:** `{Tool}Configuration` and `Configuration` views over a JSON dictionary, the alpha's first
  design, replaced by D27.
- **Where:** `Emitting/Names.cs`.

### D3. `ToolRef<TSettings, TOverrides>` carries the overrides type as well

- **Context:** the spec sketched `ToolRef<TSettings>`. Configuring a tool's stored defaults needs its generated
  overrides record, a type the library cannot name.
- **Decision:** `ToolDefaults.ConfigureAsync(id, CompanyToolset.Tools.FindCompanies, d => d with { Limit = 2 })`
  infers both types from the reference. The overrides implement `IToolOverrides<TSettings>` and `IStorable<TSelf>`,
  so nothing creates or reads them by reflection.
- **Alternatives:** a generic `ConfigureAsync<TOverrides>` the caller spells out; a generated method per tool.
- **Where:** `Tools/ToolRef.cs`, `Settings/ToolDefaults.cs`.

### D4. Typed runtime members are C# 14 extension blocks

- **Context:** `AgentRuntime<TAgent>` is a library type; its typed members need the agent's generated `Settings`.
- **Decision:** the generator emits `extension(AgentRuntime<TAgent> runtime) { ... }` with `LoadSettingsAsync`,
  `SaveSettingsAsync`, `ConfigureAsync`, `ResolveChatOptions` and `ResolveChatOptionsAsync`. They call hidden
  `...Core` and `...CoreAsync` methods of `AgentRuntime`, so the logic lives and is tested in the library. Consumers
  need C# 14, the default of .NET 10.
- **Alternatives:** classic `this` extension methods, which would let C# 13 consumers compile; a generated runtime
  subclass per agent, which the container would have to know.
- **Where:** `Emitting/AgentEmitter.cs`, `Agents/AgentRuntime.cs`.

### D5. Settings picks and chat-option picks are read only by the generator

- **Context:** `WithSettings<T>().Configurable(s => s.Limit)` and `WithChatOptions().Configurable(o => o.Temperature)`
  name the configurable members.
- **Decision:** the generator reads the `Configurable` calls from the constructor and emits the `Overrides` and
  `Settings` records; at runtime `Configurable` returns its builder and its lambda never runs. The lambdas are
  `Func<T, object?>`, not expression trees, so nothing is built for them at runtime. The configurable set exists
  once, in generated types. Tool picks are different: their calls run (D29).
- **Alternatives:** recording the picks at runtime, which duplicates the set and needs expression trees under AOT.
- **Where:** `Parsing/ConstructorChains.cs` (`GetPickedProperty`), `Toolsets/ToolBuilder.cs`,
  `Agents/ChatOptionsBuilder.cs`.

### D6. Generic toolsets and agents are not generated

- **Context:** a generic toolset's tools depend on its type arguments, which only the closing site knows.
- **Decision:** discovery skips generic classes. Their misuse surfaces through the runtime guards (a toolset without
  a generated binding throws when asked for a tool), which the runtime-guard tests use on purpose.
- **Alternatives:** generating per closed type found in `UseToolset<T>` calls.
- **Where:** `Parsing/Discovery.cs`, `tests/Typeshift.Tests/Agents/Support/RuntimeGuardAgents.cs`.

### D7. Tools are keyed by name, toolsets by nothing

- **Context:** the spec asked whether toolsets need a key besides the type name.
- **Decision:** no. Stored settings, the catalog and the tool lists address tools by their lower_snake_case name,
  which is unique host-wide (TSH007), so a toolset can move or be renamed without touching stored data.
- **Alternatives:** a `WithKey("...")` on the toolset.
- **Where:** `Tools/ToolName.cs`.

### D8. Diagnostics beyond the spec's list

- **Decision:**
  - TSH012 reports a tool name that is not lower_snake_case. The runtime does not check it again: only generated
    toolsets have bindings, and the generator has already reported the name.
  - TSH013 reports settings less accessible than their toolset, whose generated API exposes them: public for a
    public toolset, internal or wider for any other; generating narrower members instead would cascade into every
    agent that uses the tool.
  - TSH014 reports a configurable member whose type the generated records cannot store (D27).
  - TSH007 is reported on the first own declaration, or on `AddTypeshift()` when the clash is only among references.
  - The spec's agent-name diagnostics went with D31; including a foreign tool and configuring a tool the agent does
    not offer became compiler errors (D29, D30).
- **Where:** `Diagnostics/Descriptors.cs`, `docs/guide/diagnostics.md`.

### D9. Every project that declares toolsets or agents references the package

- **Context:** NuGet does not flow analyzers to the projects that reference a consumer (`PrivateAssets` defaults
  include `analyzers`).
- **Decision:** document it, and catch the mistake with TSH011. Flowing the analyzer through `buildTransitive` would
  load the generator twice in projects that also reference the package directly.
- **Alternatives:** a `buildTransitive` targets file that adds the analyzer when it is missing; possible later.
- **Where:** `Parsing/ReferenceReader.cs`, `Diagnostics/RegistrationDiagnostics.cs`.

## Runtime

### D10. The tool catalog is a library class fed by the generated modules

- **Decision:** `ToolCatalog` lives in the library and reads the toolsets the modules register, so a host can create
  any tool by name (for agents it assembles itself). Generated code stays small and the catalog is tested once.
- **Alternatives:** a generated catalog per host, as the spec sketched.
- **Where:** `Catalogs/ToolCatalog.cs`.

### D11. Each tool call runs in a DI scope of its own

- **Context:** `FunctionInvokingChatClient` hands tools the root provider.
- **Decision:** `GeneratedToolFunction` creates a scope per call, and the `AIFunctionArguments` the tool receives
  carry it, so scoped services work per call. A container without `IServiceScopeFactory` throws, naming
  `AddTypeshift()`. Settings no longer travel through the scope (D40).
- **Alternatives:** the root provider, which makes every scoped service a singleton.
- **Where:** `Tools/GeneratedToolFunction.cs`.

### D12. Nullable parameters are nullable at the schema root only

- **Context:** `AIJsonUtilities.CreateJsonSchema` does not mark a nullable reference parameter as nullable.
- **Decision:** the generator records the nullability and the library widens the parameter's root `type` to
  `[type, "null"]`. Nested nullability follows Microsoft.Extensions.AI unchanged, and the schema test compares
  against `AIFunctionFactory` for that reason.
- **Alternatives:** rewriting nested schemas, which diverges from what providers already accept.
- **Where:** `Json/AgentJson.cs`.

### D17. The model provider interface

- **Decision:** `IChatClientProvider.GetChatClientAsync(string model, ct)` maps a model name to a client.
  `AgentRuntime.CreateChatClientAsync(options)` passes it `options.ModelId`, which the agent's code sets and its
  `Settings.Model` overrides (D34), and wraps the client: conversation tracking outside, `FunctionInvokingChatClient`
  inside. A missing model throws `ConfigurationRequiredException`.
- **Alternatives:** keyed `IChatClient` registrations, which cannot depend on stored settings.
- **Where:** `Agents/IChatClientProvider.cs`, `Agents/AgentRuntime.cs`.

## Conversations

### D18. The port of the owner's conversation tracking

- **Decision:**
  - `TrackedConversationManager` became `ConversationTracker`; the initialize and resume option classes became one
    internal `ConversationStart` record; `ProviderModel` became the plain model name the provider maps.
  - The provider receives the options without `ConversationId`. The owner's code passed them unchanged; but the
    tracker resends the conversation itself, and a provider that reads the id as server-side state (the OpenAI
    Responses API, say) would reject it or double the history.
  - A failed stream is stored with the updates that arrived and no `ErrorContent`, as in the owner's code; a failed
    unary call gets the `ErrorContent`.
  - A session that started a new conversation decides at its end whether to create it or append to it, so two
    overlapping first sessions both land; appending to a conversation deleted meanwhile leaves it deleted. In one
    process the sessions of a conversation store one at a time; across processes the later save wins.
  - Storing a finished session does not observe the call's token, so cancelled calls are stored too.
  - Resuming drops function calls that never got a result (a call cancelled mid-tool), which providers reject.
  - Subscribers are awaited until their limit token fires instead of on a second timer. The behavior is the same, and
    tests that advance a `FakeTimeProvider` cannot race the wait.
- **Alternatives:** keeping the option classes public, which adds API the alpha does not need.
- **Where:** `Conversations/`.

## Repository

### D19. Sealing

- **Decision:** generated classes, assertion classes and classes whose inheritance would be a bug (`AgentRuntime<T>`,
  `ToolCatalog`, `ToolDefaults`, `InMemoryTypeshiftStore<TId>`) are sealed; the rest stays open, as the owner's code
  does.

### D20. CS1591 is suppressed in test projects only

- **Decision:** `tests/.editorconfig` turns CS1591 off, with a comment saying why: test classes are public for the
  test framework but are not API. Everything else builds with `TreatWarningsAsErrors` and documentation on.

### D21. The sample uses only a scripted client

- **Context:** the spec allowed an optional real provider behind an environment variable.
- **Decision:** provider SDKs are out of scope, so the sample ships only its scripted client and shows where a real
  one plugs in (`IChatClientProvider`).

### D22. The package metadata points at the real repository

- **Context:** the spec names the repository `agentic-dotnet`; the work happens in `weboost-at/Typeshift`, which now
  publishes the docs site and the packages.
- **Decision:** `RepositoryUrl` is `https://github.com/weboost-at/Typeshift` and `PackageProjectUrl` the docs site, so
  SourceLink, nuget.org and the docs agree.

## Persistence and typed settings, decided with the owner

### D25. One store, `ITypeshiftStore<TId>`, of documents that serialize themselves

- **Context:** the alpha had four store interfaces (agent configuration, agent definitions, tool defaults,
  conversations). The owner asked for one atomic interface whose data serializes itself, with a generic load that
  returns the typed object, and an id type the host chooses.
- **Decision:** `ITypeshiftStore<TId>` has `LoadAsync<TData>(id, ct)` and `SaveAsync<TData>(id, data, ct)`, where
  `TData : IStorable<TData>`: `Serialize()` and a static abstract `Deserialize(string)`. The store keys a document
  by its type and the id. It may keep the JSON as it is or read the typed properties into columns of its own; the
  library never creates ids, it passes on the ones the host gives it. `InMemoryTypeshiftStore<TId>` keeps JSON in a
  dictionary.
- **Alternatives:** a store of JSON strings keyed by a string, which hides the types a Marten or EF Core store would
  map; one interface per document kind, the alpha's design.
- **Consequence:** the documents are a closed set of known types (each agent's `Settings`, each tool's `Overrides`,
  `StoredConversation`), so a generator for Marten or EF Core mappings can enumerate them later.
- **Where:** `Persistence/ITypeshiftStore.cs`, `Persistence/IStorable.cs`, `Persistence/InMemory/`.

### D26. The store is the persistence feature flag

- **Decision:** `AddTypeshiftStore<TStore, TId>()` registers the store as a singleton, plus an internal bridge that
  stores conversations. Without it, everything that works from code works (`ResolveChatOptions`, tool calls,
  conversation tracking in memory), and every API that loads or saves throws an `InvalidOperationException` naming
  `AddTypeshiftStore<TStore, TId>()`, also when the store's `TId` differs from the one asked for.
- **Alternatives:** a null-object store that silently stores nothing, which hides a missing registration until data
  is lost.
- **Where:** `Persistence/Stores.cs`, `DependencyInjection/TypeshiftServiceCollectionExtensions.cs`.

### D27. Settings are generated records, typed and closed

- **Context:** the owner asked for typed settings throughout, no reflection, no `object` catch-all, and one settings
  object per agent holding only what may change, usable as a contract (a settings UI, say).
- **Decision:**
  - per agent, a nested `Settings` record: `Model`, every configurable chat option, and per tool with configurable
    settings a property of the tool's `{Tool}Overrides` record. Every value is nullable; null keeps the layer below.
  - The records serialize through hidden `StorableJson` helpers the generated code calls member by member, so no
    `JsonSerializerContext` is needed and nothing reflects. The JSON is camelCase, tools nest under `"tools"` by tool
    name, nulls are left out, and a missing or mistyped value reads as null.
  - Configurable members are limited to types a UI can edit and the helpers can write (TSH014).
- **Alternatives:** a generated `JsonSerializerContext` per agent, impossible because source generators do not see
  each other's output; the alpha's `StoredSettings` dictionary with views over it, which the owner rejected as
  untyped.
- **Where:** `Emitting/StorableEmitter.cs`, `Parsing/Storage.cs`, `Persistence/StorableJson.cs`,
  `Agents/IAgentSettings.cs`, `Settings/IToolOverrides.cs`.

### D28. Tool defaults are stored under the agent's id

- **Context:** the owner chose one id for an agent's settings and for the defaults of the tools it runs.
- **Decision:** `ResolveChatOptionsAsync(id)` loads the agent's `Settings` and each tool's `{Tool}Overrides` under the
  same id, all in parallel. Stored defaults therefore apply to every agent run under that id, and two agents'
  settings never collide, since each agent has its own `Settings` type.
- **Alternatives:** a separate id for tool defaults, which every resolve call would have to pass.
- **Where:** `Agents/AgentRuntime.cs` (`LoadToolDefaultsAsync`), `Tools/OverridableToolBinding.cs`.

### D29. Tools are picked from a generated list, included or excluded

- **Context:** `UseToolset<T>(params Delegate[])` accepted any delegate and checked it at runtime and in TSH007. Its
  successor chained a generated method per tool after `UseToolset<T>()`, which could only include; the owner asked
  for include and exclude, picked from a typed list of the toolset's tools, never a delegate.
- **Decision:** `UseToolset<T>()` offers every tool; `.Include(t => t.A).Include(t => t.B)` offers only those, in
  that order; `.Exclude(t => t.A)` offers all but those. `Include` returns a chain that only includes and `Exclude`
  one that only excludes, so one call never mixes them. The lambda's parameter is the library's `ToolList<T>`, to
  which the generator adds one C# 14 extension property per tool returning the tool's `ToolRef`. A tool of another
  toolset has no property on this list: CS9286. The lambdas run at runtime, and a `ToolRef` of another toolset
  throws an `ArgumentException`; in the generator's own compilation the properties do not bind (they do not exist
  yet), so the parser reads the method and the property name of each pick from syntax.
- **Alternatives:** method groups (`T.Method`), matched by `Delegate.Method`; a generated method per tool chained on
  the selection, include-only; one generated `Include` and `Exclude` per toolset, where a library list typed by the
  toolset does the same.
- **Where:** `Tools/ToolsetUse.cs`, `Tools/ToolList.cs`, `Parsing/AgentParser.cs` (`GetPicks`),
  `Emitting/ToolsetEmitter.cs` (`WriteToolList`).

### D30. `Configure{Tool}()` is a generated builder per tool of an agent

- **Context:** the alpha's `WithToolSettings(object)` accepted anything and checked at runtime and in TSH012. Its
  successors, one `WithToolSettings(TSettings)` overload per settings type and then a `WithToolSettings()` picker in
  front of a builder per tool, made the agent build a whole settings object or pick the tool twice; the owner asked
  for one entry point per tool.
- **Decision:** the generator emits a private `Configure{Tool}()` per offered tool that has settings, returning a
  nested builder, `ConfigureSearchTickets().WithLimit(5)`. Each tool's builder has one
  `With{Member}` per settable member of its settings record and writes `(field ?? new()) with { Member = value }`
  into a field per tool; the hidden `GetToolSettings(ToolName)` override returns it. The agent's values still act as
  a whole object over the code defaults, so the layering is unchanged. A tool the agent does not offer has no method:
  a compiler error. The fields are read when options are resolved, so the call may come before or after
  `UseToolset`. Referenced toolsets' settings members are read from the settings type in metadata.
- **Alternatives:** one overload per settings type, which applied one object to every tool reading the type; a
  `WithToolSettings()` picker, a step that says nothing; a lambda over the settings record, which is the nested
  builder D41 rules out.
- **Where:** `Emitting/AgentEmitter.cs`, `Agents/AgentDefinition.cs`, `Parsing/ReferenceReader.cs`.

### D31. Agent names are optional and need not be unique

- **Context:** the owner asked to drop the unique-name requirement.
- **Decision:** `WithName` is optional; without it an agent goes by its class name. The name only labels the
  execution context of its conversations. Stored data is keyed by type and id, never by name.
- **Where:** `Agents/AgentDefinition.cs`.

### D32. Agents as data are dropped

- **Context:** stored agents (`StoredAgent`, export, the agent catalog) required unique names and a schema of
  their own. The owner chose to drop them; hosts decide how to store anything beyond settings.
- **Decision:** removed, with the spec's behaviors 38 to 40.
- **Where:** nothing left; [status](./status.md) records the dropped behaviors.

### D33. Conversation ids are parsed into the store's id type

- **Context:** `ChatOptions.ConversationId` is a string; the store's ids are `TId`.
- **Decision:** `AddTypeshiftStore` requires `TId : IParsable<TId>` and the tracker parses the id with the invariant
  culture. A call without an id is tracked live but not stored; an id that does not parse is neither resumed nor
  stored. A `StoredConversation` is just another `IStorable` document.
- **Alternatives:** a separate conversation id type, which every host would have to register twice.
- **Where:** `Conversations/ConversationStore.cs`, `Conversations/ConversationTracker.cs`.

### D34. The model is a setting

- **Decision:** every agent's `Settings` has `Model`, which overrides `ChatOptions.ModelId`. The runtime picks the
  client from the resolved options, so a model stored per id or passed per run needs no further API.
- **Where:** `Emitting/AgentEmitter.cs`, `Agents/AgentRuntime.cs`.

### D35. The hidden public surface generated code calls

- **Decision:** generated code in a consumer cannot reach internals, so what only it calls is public and
  `[EditorBrowsable(Never)]`: `AgentDefinition.GetToolSettings`, the `AgentRuntime.*Core*`
  methods, `StorableJson`, `Toolset.GetBinding` and `ModuleRegistration`. The base type generated code derives
  from (`GeneratedToolFunction`), the `ToolBinding` it creates and the stamps in `Typeshift.Modules` stay visible.
- **Alternatives:** `InternalsVisibleTo` for every consumer, which is not possible.

### D36. Diagnostics renumbered before the first release

- **Decision:** with nothing shipped, the IDs were renumbered to close the gaps D29 to D31 left: TSH001 to TSH014.
  After the first release IDs never change; a retired one stays unused.
- **Where:** `AnalyzerReleases.Unshipped.md`.

## Distribution

### D37. Releases are tags, published through trusted publishing

- **Context:** the owner distributes the library through NuGet.
- **Decision:** a `v*` tag runs `release.yml`: build, test, pack with package validation, run the package consumer,
  then push to nuget.org and create the GitHub release. nuget.org trusted publishing exchanges the run's GitHub
  identity for a key valid for an hour, so no API key is stored; the `nuget` environment gates the push. MinVer
  versions from the tag. Symbols ship as a `.snupkg`, sources through SourceLink.
- **Alternatives:** a stored `NUGET_API_KEY` secret, which lives until someone rotates it; versions in the project
  file, which a release commit has to bump.
- **Where:** `.github/workflows/release.yml`, `Directory.Build.props`, `docs/contributing/releasing.md`.

### D38. The library is named Typeshift; the props follow the package ID

- **Context:** the working name, `Agentic`, is taken on nuget.org by another author. NuGet imports
  `buildTransitive/{PackageId}.props` only, and that file opts consumers into the interceptor namespace.
- **Decision:** the owner named the library Typeshift, for what the generator does: plain C# toolsets and agents
  shift into typed, reflection-free code at compile time. It names the package ID, the root namespace, the
  repository, `AddTypeshift()`, `AddTypeshiftStore<TStore, TId>()` and the diagnostic prefix `TSH`. The props file is
  packed under `$(PackageId).props`, so the ID, the props and the interceptor namespace cannot disagree; the package
  consumer fails the build when they do.
- **Alternatives:** keeping `Agentic` under an owner prefix (`Weboost.Agentic`), which leaves the namespace and the
  search results shared with the other package; `Agentype`, taken on PyPI; `Agentric`, one letter from `Agentic`;
  `Agentwright`, `Caddie` and other names that say nothing about types or compile time.
- **Where:** `src/Typeshift/Typeshift.csproj`, `Emitting/Names.cs` (`GeneratedNamespace`), `Parsing/KnownTypes.cs`.

### D39. The package has a README of its own

- **Decision:** `src/Typeshift/PACKAGE.md` is the package's README on nuget.org: what it is, how to install, links to
  the docs. It has no code, so it cannot drift; the repository README carries the compiled quickstart, whose
  snippet links nuget.org would show as raw HTML.
- **Where:** `src/Typeshift/PACKAGE.md`.

## API shape, decided with the owner

### D40. A tool receives its settings as a parameter

- **Context:** tools read their settings with `services.GetToolSettings<T>()` from a scoped accessor the generated
  function filled, and TSH009 forbade a settings parameter. The owner asked for the settings to arrive as a parameter
  the model cannot set.
- **Decision:** a parameter of the type given to `WithSettings<T>()` is bound like `IServiceProvider`: the generated
  function passes the settings it was created with and leaves the parameter out of the schema, so the model never
  sees or sets it. `GetToolSettings<T>()` and the accessor are gone; TSH009 now warns about a tool with settings but
  no such parameter, whose settings would reach nothing.
- **Alternatives:** keeping `GetToolSettings<T>()` next to the parameter, two ways to do one thing.
- **Where:** `Parsing/ToolsetParser.cs` (`ParameterKind.Settings`), `Emitting/ToolsetEmitter.cs`.

### D41. One builder chain, never a builder inside a lambda

- **Context:** `WithSettings<T>(s => s.Configurable(...))`, `WithChatOptions(options, p => p.Configurable(...))` and
  `UseToolset<T>(tools => tools.A())` each opened a second builder inside a lambda, and chat options needed a
  `ChatOptions` instance built by hand.
- **Decision:** every declaration is one flat chain. `WithSettings<T>()` returns `ToolBuilder<TSettings>`, whose
  `Configurable` is typed by the settings, and `WithConfigurableSettings<T>()` its counterpart with `NotConfigurable`
  (D43); either comes last in the tool's chain.
  `WithChatOptions()` returns a `ChatOptionsBuilder` with a `With...` per chat option worth setting in code and
  `Configurable`. `UseToolset<T>()` returns the use itself, chained with `Include` or `Exclude` (D29); nothing
  chained offers the whole toolset, so the duplicate check runs when the agent's tools are first read. Plain generics
  carry the chains where the library knows the types; the generator writes only what it alone knows, the tool lists
  and `Configure{Tool}()` (D29, D30).
- **Alternatives:** generating the `Configurable` methods per settings type, which a generic builder gives for free
  and before the generator runs; `WithName` and `WithDescription` on `ToolBuilder<TSettings>` too, flexibility no
  chain needs.
- **Where:** `Toolsets/ToolBuilder.cs`, `Agents/ChatOptionsBuilder.cs`, `Agents/AgentDefinition.cs`,
  `Tools/ToolsetUse.cs`.

### D42. Instructions come from a virtual method

- **Decision:** `AgentDefinition.GetInstructions()` returns the agent's instructions, null by default; the
  `ChatOptionsBuilder` has no `WithInstructions`, so there is one place for them. It is called whenever a run's
  options are resolved, never from the constructor, so an override may read anything the derived constructor set and
  may compute the text.
- **Alternatives:** a `WithInstructions` call, which puts long prompts into the constructor; calling it once in the
  base constructor, before the derived constructor has run.
- **Where:** `Agents/AgentDefinition.cs` (`CreateChatOptions`).

### D43. Configurable members are an allow list unless `WithConfigurableSettings` says otherwise

- **Context:** a settings record whose members should almost all change at runtime needed one `Configurable` per
  member.
- **Decision:** `WithSettings<T>()` keeps every member in code; `Configurable(s => s.X)` allows the members it names.
  `WithConfigurableSettings<T>()` allows every settable member but the obsolete ones and those
  `NotConfigurable(s => s.X)` names. Each returns a builder that only continues in its own way, so the two styles
  never mix. There is no "none configurable" call: plain `WithSettings<T>()` is that. Under
  `WithConfigurableSettings<T>()`, a member whose type settings cannot store is reported as TSH014 on that call until
  it is kept out, so it never silently stays code-only. The mode is part of the entry call, not a call of its own,
  so the chain reads from the longest line down, as the owner orders it.
- **Alternatives:** an opt-out default, which would put every new member into stored contracts unasked; skipping
  unstorable members silently; a separate `AllConfigurable()` call after `WithSettings<T>()`, a short line above
  the longer exclusions; an enum argument on `WithSettings<T>`; exclusions before a closing `AllConfigurable()`.
- **Where:** `Toolsets/ToolBuilder.cs`, `Parsing/ToolsetParser.cs` (`ParseSettings`).

### D44. Each module registers the JSON contexts of its tools

- **Context:** under Native AOT, the types a tool takes and returns need source-generated metadata, and every host
  added its context by hand with `services.Configure<AgentJsonOptions>(...)`. A host cannot name an `internal`
  context of a referenced toolset assembly at all.
- **Decision:** the generator reads every `JsonSerializerContext` the module can name and the types its
  `[JsonSerializable]` attributes list. A context that lists a parameter or result type (awaited) of the assembly's
  own tools is added by the module's `Register`, so `AddTypeshift()` registers it. The module adds it with
  `PostConfigure`, so the resolvers the host configures come first, then the contexts of the own module, then those
  of the referenced ones, then the library's `AgentJsonContext`; a context the host added itself is not added twice.
- **Alternatives:** registering every context of the assembly, which lets one written for other JSON, with other
  options, take over tool types; an `AddTypeshift(params IJsonTypeInfoResolver[])` overload, explicit but still a
  line per host and no help for referenced assemblies; a context generated per assembly, impossible because source
  generators do not see each other's output (D27).
- **Where:** `Parsing/Discovery.cs` (`ReadJsonContext`), `AgentGenerator.cs` (`GetJsonContexts`),
  `Emitting/ModuleEmitter.cs`, `Modules/ModuleRegistration.cs` (`AddJsonContext`).

## Withdrawn

| ID | Was | Replaced by |
|---|---|---|
| D13 | Agents resolve through one path, code or stored. | D32: agents as data were dropped; one resolve path remains, `AgentDefinition.CreateChatOptions`. |
| D14 | Stored agents get generated names. | D32. |
| D15 | Listing and exporting agents. | D32; the agent catalog went with it. |
| D16 | Optional store interfaces, tool defaults loaded in batches. | D25, D26; tool defaults load one document per tool, in parallel (D28). |
| D23 | Run tool settings are settings objects. | D27: run settings are the agent's generated `Settings`, whose nullable members override only what a run sets. |
| D24 | Selected tools are matched by their method. | D29: tools are picked from a generated list and matched by name. |
