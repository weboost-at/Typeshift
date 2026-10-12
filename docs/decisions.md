# Decisions

Why the code is shaped the way it is. Each entry gives the context, the decision, the alternatives that lost and where
the decision lives in the code. IDs are stable: code comments cite them (`// D25: ...`), and a decision that no longer
holds stays as a withdrawn stub pointing to what replaced it. "Spec" means the alpha's design document; "owner" means
the maintainer, whose review shaped D25 to D36.

Before changing code an entry names, read the entry. To change the decision itself, edit the entry in the same
commit, or add a new one and withdraw the old.

## Registration and the generator

### D1. `AddAgentic()` exists only as an intercepted call

- **Context:** a generated `AddAgentic()` per assembly is ambiguous (CS0121) wherever `InternalsVisibleTo` exposes
  another assembly's internals, and every test project does.
- **Decision:** the library ships one public `AddAgentic()` that throws an `InvalidOperationException` saying what to
  check; the generator intercepts every call in the project that makes it. The method lives in the
  `Microsoft.Extensions.DependencyInjection` namespace, like every `Add...` method, so hosts need no extra using.
- **Alternatives:** a generated method with a per-assembly name (`AddAgenticForMyApp()`), which leaks the assembly
  name into every host; module initializers, which run on load and cannot see the service collection.
- **Where:** `DependencyInjection/AgenticServiceCollectionExtensions.cs`, `Emitting/InterceptorEmitter.cs`,
  `buildTransitive/Agentic.props`.

### D2. The generated names

- **Decision:**
  - per toolset: `{Toolset}.Tools.{Tool}`, a `ToolRef` or `ToolRef<TSettings, TOverrides>`; per tool with
    configurable settings a nested `{Tool}Overrides` record; a `{Toolset}Selection` class whose extension block adds
    one selection method per tool to `ToolSelection<TToolset>`;
  - per agent: the nested `Settings` record, private `WithToolSettings` overloads, and a `{Agent}RuntimeExtensions`
    class with the typed members of `AgentRuntime<TAgent>`; a nested agent's class name joins its containers'
    names, so twins in two classes do not clash;
  - per assembly: `Agentic.Generated.{Assembly}Module`, with every character of the assembly name that is not a letter
    or digit replaced by `_` (`Agentic.Samples.Console` gives `Agentic_Samples_ConsoleModule`);
  - the interceptor: a `file static class AddAgenticInterceptor` in `Agentic.Generated`;
  - a generated member named like a member next to it gets a suffix: a tool `select` is selected with `SelectTool()`,
    since `Select` is the selection's own method.
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
  `...CoreAsync` methods of `AgentRuntime`, so the logic lives and is tested in the library. Consumers need C# 14,
  the default of .NET 10.
- **Alternatives:** classic `this` extension methods, which would let C# 13 consumers compile; a generated runtime
  subclass per agent, which the container would have to know.
- **Where:** `Emitting/AgentEmitter.cs`, `Agents/AgentRuntime.cs`.

### D5. Settings picks and chat-option picks are read only by the generator

- **Context:** `WithSettings<T>(s => s.Configurable(x => x.Limit))` and `WithChatOptions(o, p => p.Configurable(...))`
  name the configurable members.
- **Decision:** the generator reads the lambdas from the constructor and emits the `Overrides` and `Settings`
  records; at runtime the pickers are never invoked. The configurable set exists once, in generated types, without
  expression trees. Tool selection is different: its lambda runs (D29).
- **Alternatives:** recording the picks at runtime, which duplicates the set and needs expression trees under AOT.
- **Where:** `Parsing/ConstructorChains.cs`, `Settings/ConfigurableSettingsPicker.cs`.

### D6. Generic toolsets and agents are not generated

- **Context:** a generic toolset's tools depend on its type arguments, which only the closing site knows.
- **Decision:** discovery skips generic classes. Their misuse surfaces through the runtime guards (a toolset without
  a generated binding throws when asked for a tool), which the runtime-guard tests use on purpose.
- **Alternatives:** generating per closed type found in `UseToolset<T>` calls.
- **Where:** `Parsing/Discovery.cs`, `tests/Agentic.Tests/Agents/Support/RuntimeGuardAgents.cs`.

### D7. Tools are keyed by name, toolsets by nothing

- **Context:** the spec asked whether toolsets need a key besides the type name.
- **Decision:** no. Stored settings, the catalog and the selection methods address tools by their lower_snake_case
  name, which is unique host-wide (AGT007), so a toolset can move or be renamed without touching stored data.
- **Alternatives:** a `WithKey("...")` on the toolset.
- **Where:** `Tools/ToolName.cs`.

### D8. Diagnostics beyond the spec's list

- **Decision:**
  - AGT012 reports a tool name that is not lower_snake_case.
  - AGT013 reports settings less accessible than their public toolset, whose generated API exposes them; generating
    narrower members instead would cascade into every agent that uses the tool.
  - AGT014 reports a configurable member whose type the generated records cannot store (D27).
  - AGT007 is reported on the first own declaration, or on `AddAgentic()` when the clash is only among references.
  - The spec's agent-name diagnostics went with D31; selecting a foreign tool and passing unread tool settings
    became compiler errors (D29, D30).
- **Where:** `Diagnostics/Descriptors.cs`, `docs/diagnostics.md`.

### D9. Every project that declares toolsets or agents references the package

- **Context:** NuGet does not flow analyzers to the projects that reference a consumer (`PrivateAssets` defaults
  include `analyzers`).
- **Decision:** document it, and catch the mistake with AGT011. Flowing the analyzer through `buildTransitive` would
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
- **Decision:** `GeneratedToolFunction` creates a scope per call and puts the resolved settings in the scope's
  accessor, so `GetToolSettings<T>()` and scoped services work per call. A container without
  `IServiceScopeFactory` throws, naming `AddAgentic()`.
- **Alternatives:** an `AsyncLocal` for the settings, which is static mutable state.
- **Where:** `Tools/GeneratedToolFunction.cs`, `Tools/ToolSettingsAccessor.cs`.

### D12. Nullable parameters are nullable at the schema root only

- **Context:** `AIJsonUtilities.CreateJsonSchema` does not mark a nullable reference parameter as nullable.
- **Decision:** the generator records the nullability and the library widens the parameter's root `type` to
  `[type, "null"]`. Nested nullability follows Microsoft.Extensions.AI unchanged, and the schema test compares
  against `AIFunctionFactory` for that reason.
- **Alternatives:** rewriting nested schemas, which diverges from what providers already accept.
- **Where:** `Json/AgentJson.cs`.

### D13. Withdrawn: agents resolve through one path, code or stored

Agents as data were dropped (D32). One resolve path remains: `AgentDefinition.CreateChatOptions`.

### D14. Withdrawn: stored agents get generated names

Dropped with D32.

### D15. Withdrawn: listing and exporting agents

Dropped with D32; the agent catalog went with it.

### D16. Withdrawn: optional store interfaces, tool defaults loaded in batches

Replaced by D25 and D26. Tool defaults now load one document per tool, in parallel (D28).

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
    overlapping first sessions both land; appending to a conversation deleted meanwhile leaves it deleted.
  - Storing a finished session does not observe the call's token, so cancelled calls are stored too.
  - Resuming drops function calls that never got a result (a call cancelled mid-tool), which providers reject.
  - Subscribers are awaited until their limit token fires instead of on a second timer. The behavior is the same, and
    tests that advance a `FakeTimeProvider` cannot race the wait.
- **Alternatives:** keeping the option classes public, which adds API the alpha does not need.
- **Where:** `Conversations/`.

## Repository

### D19. Sealing

- **Decision:** generated classes, assertion classes and classes whose inheritance would be a bug (`AgentRuntime<T>`,
  `ToolCatalog`, `ToolDefaults`, `InMemoryAgenticStore<TId>`) are sealed; the rest stays open, as the owner's code
  does.

### D20. CS1591 is suppressed in test projects only

- **Decision:** `tests/.editorconfig` turns CS1591 off, with a comment saying why: test classes are public for the
  test framework but are not API. Everything else builds with `TreatWarningsAsErrors` and documentation on.

### D21. The sample uses only a scripted client

- **Context:** the spec allowed an optional real provider behind an environment variable.
- **Decision:** provider SDKs are out of scope, so the sample ships only its scripted client and shows where a real
  one plugs in (`IChatClientProvider`).

### D22. The repository URL in the package metadata

- **Context:** the spec names the repository `agentic-dotnet`; the work happened in `weboost-at/Agentic`.
- **Decision:** `Directory.Build.props` keeps `https://github.com/weboost-at/agentic-dotnet`, the spec's name, and
  `docs/renaming.md` lists it.

## Runtime, decided in the review pass

### D23. Withdrawn: run tool settings are settings objects

Run settings are now the agent's generated `Settings`, whose members are nullable, so only the members a run sets
override (D27). The trap this entry documented, a non-nullable member always overriding, is gone.

### D24. Withdrawn: selected tools are matched by their method

Replaced by D29: tools are selected through generated methods and matched by name.

## Persistence and typed settings, decided with the owner

### D25. One store, `IAgenticStore<TId>`, of documents that serialize themselves

- **Context:** the alpha had four store interfaces (agent configuration, agent definitions, tool defaults,
  conversations). The owner asked for one atomic interface whose data serializes itself, with a generic load that
  returns the typed object, and an id type the host chooses.
- **Decision:** `IAgenticStore<TId>` has `LoadAsync<TData>(id, ct)` and `SaveAsync<TData>(id, data, ct)`, where
  `TData : IStorable<TData>`: `Serialize()` and a static abstract `Deserialize(string)`. The store keys a document
  by its type and the id. It may keep the JSON as it is or read the typed properties into columns of its own; the
  library never creates ids, it passes on the ones the host gives it. `InMemoryAgenticStore<TId>` keeps JSON in a
  dictionary.
- **Alternatives:** a store of JSON strings keyed by a string, which hides the types a Marten or EF Core store would
  map; one interface per document kind, the alpha's design.
- **Consequence:** the documents are a closed set of known types (each agent's `Settings`, each tool's `Overrides`,
  `StoredConversation`), so a generator for Marten or EF Core mappings can enumerate them later.
- **Where:** `Persistence/IAgenticStore.cs`, `Persistence/IStorable.cs`, `Persistence/InMemory/`.

### D26. The store is the persistence feature flag

- **Decision:** `AddAgenticStore<TStore, TId>()` registers the store as a singleton, plus an internal bridge that
  stores conversations. Without it, everything that works from code works (`ResolveChatOptions`, tool calls,
  conversation tracking in memory), and every API that loads or saves throws an `InvalidOperationException` naming
  `AddAgenticStore<TStore, TId>()`, also when the store's `TId` differs from the one asked for.
- **Alternatives:** a null-object store that silently stores nothing, which hides a missing registration until data
  is lost.
- **Where:** `Persistence/Stores.cs`, `DependencyInjection/AgenticServiceCollectionExtensions.cs`.

### D27. Settings are generated records, typed and closed

- **Context:** the owner asked for typed settings throughout, no reflection, no `object` catch-all, and one settings
  object per agent holding only what may change, usable as a contract (a settings UI, say).
- **Decision:**
  - per agent, a nested `Settings` record: `Model`, every configurable chat option, and per tool with configurable
    settings a property of the tool's `{Tool}Overrides` record. Every value is nullable; null keeps the layer below.
  - The records serialize through hidden `StorableJson` helpers the generated code calls member by member, so no
    `JsonSerializerContext` is needed and nothing reflects. The JSON is camelCase, tools nest under `"tools"` by tool
    name, nulls are left out, and a missing or mistyped value reads as null.
  - Configurable members are limited to types a UI can edit and the helpers can write (AGT014).
- **Alternatives:** a generated `JsonSerializerContext` per agent, impossible because source generators do not see
  each other's output; the alpha's `StoredSettings` dictionary with views over it, which the owner rejected as
  untyped.
- **Where:** `Emitting/StorableEmitter.cs`, `Parsing/Storage.cs`, `Persistence/StorableJson.cs`,
  `Agents/IAgentSettings.cs`, `Settings/IToolOverrides.cs`.

### D28. Tool defaults are stored under the agent's id

- **Context:** the owner chose one id for an agent's settings and for the defaults of the tools it runs.
- **Decision:** `ResolveChatOptionsAsync(id)` loads the agent's `Settings` and each tool's `{Tool}Overrides` under the
  same id, the tools in parallel. Stored defaults therefore apply to every agent run under that id, and two agents'
  settings never collide, since each agent has its own `Settings` type.
- **Alternatives:** a separate id for tool defaults, which every resolve call would have to pass.
- **Where:** `Agents/AgentRuntime.cs` (`LoadToolDefaultsAsync`), `Tools/OverridableToolBinding.cs`.

### D29. Tools are selected through generated methods

- **Context:** `UseToolset<T>(params Delegate[])` accepted any delegate and checked it at runtime and in AGT007.
- **Decision:** `UseToolset<T>(tools => tools.FindCompanies().CreateCompanyProfile())`. The generator adds one
  method per tool to `ToolSelection<T>` through an extension block, each calling the hidden `Select(name)`. A tool of
  another toolset has no method on this selection, so selecting it is CS1929. The lambda runs at runtime; in the
  generator's own compilation it does not bind (the methods do not exist yet), so the parser reads the call from
  syntax and the overload's candidate symbols.
- **Alternatives:** method groups (`T.Method`), matched by `Delegate.Method`; a generated builder interface per agent.
- **Where:** `Tools/ToolSelection.cs`, `Parsing/AgentParser.cs` (`GetUseToolset`), `Emitting/ToolsetEmitter.cs`.

### D30. `WithToolSettings` has one generated overload per settings type

- **Context:** the alpha's `WithToolSettings(object)` accepted anything and checked at runtime and in AGT012.
- **Decision:** the generator emits a private `WithToolSettings(TSettings)` per settings type the agent's selected
  tools read, backed by a field, and overrides the hidden `GetToolSettings(ToolName)` to return it. Settings no tool
  reads have no overload: CS1503. The fields are read when options are resolved, so the call may come before or
  after `UseToolset`.
- **Where:** `Emitting/AgentEmitter.cs`, `Agents/AgentDefinition.cs`.

### D31. Agent names are optional and need not be unique

- **Context:** the owner asked to drop the unique-name requirement.
- **Decision:** `WithName` is optional; without it an agent goes by its class name. The name only labels the
  execution context of its conversations. Stored data is keyed by type and id, never by name.
- **Where:** `Agents/AgentDefinition.cs`.

### D32. Agents as data are dropped

- **Context:** stored agents (`StoredAgent`, export, the agent catalog) required unique names and a schema of
  their own. The owner chose to drop them; hosts decide how to store anything beyond settings.
- **Decision:** removed, with the spec's behaviors 38 to 40.
- **Where:** nothing left; `docs/status.md` records the dropped behaviors.

### D33. Conversation ids are parsed into the store's id type

- **Context:** `ChatOptions.ConversationId` is a string; the store's ids are `TId`.
- **Decision:** `AddAgenticStore` requires `TId : IParsable<TId>` and the tracker parses the id with the invariant
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
  `[EditorBrowsable(Never)]`: `ToolSelection.Select`, `AgentDefinition.GetToolSettings`, the `AgentRuntime.*Core*`
  methods, `StorableJson`, `Toolset.GetBinding` and `ModuleRegistration`. The base types generated code derives
  from (`GeneratedToolFunction`, `ToolBinding`) and the stamps in `Agentic.Modules` stay visible.
- **Alternatives:** `InternalsVisibleTo` for every consumer, which is not possible.

### D36. Diagnostics renumbered before the first release

- **Decision:** with nothing shipped, the IDs were renumbered to close the gaps D29 to D31 left: AGT001 to AGT014.
  After the first release IDs never change; a retired one stays unused.
- **Where:** `AnalyzerReleases.Unshipped.md`.
