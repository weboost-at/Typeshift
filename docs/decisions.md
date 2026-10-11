# Decisions

Decisions taken during the alpha, each with its context, the decision and the alternatives. The ones the owner should
review first are listed at the end of `docs/status.md`. "Spec" means the alpha's design document and behavior list.

## Registration and the generator

### D1. `AddAgentic()` exists only as an intercepted call

- **Context:** a generated `AddAgentic()` per assembly is ambiguous (CS0121) wherever `InternalsVisibleTo` exposes
  another assembly's internals, and every test project does.
- **Decision:** the library ships one public `AddAgentic()` that throws an `InvalidOperationException` saying what to
  check; the generator intercepts every call in the project that makes it. The method lives in the
  `Microsoft.Extensions.DependencyInjection` namespace, like every `Add...` method, so hosts need no extra using.
- **Alternatives:** a generated method with a per-assembly name (`AddAgenticForMyApp()`), which leaks the assembly
  name into every host; module initializers, which run on load and cannot see the service collection.

### D2. The generated names

- **Decision:**
  - per toolset: `{Toolset}.Tools.{Tool}` as `ToolRef` or `ToolRef<TSettings, TConfiguration>`, and per tool with
    settings a nested `{Tool}Configuration` view;
  - per agent: the `Name` constant, `ToolNames`, the nested `Configuration` view, and a
    `{Agent}ConfigurationExtensions` class with `ConfigureAsync` on `AgentRuntime<TAgent>`;
  - per assembly: `Agentic.Generated.{Assembly}Module`, with every character of the assembly name that is not a letter
    or digit replaced by `_` (`Agentic.Samples.Console` gives `Agentic_Samples_ConsoleModule`);
  - the interceptor: a `file static class AddAgenticInterceptor` in `Agentic.Generated`.
- **Alternatives:** a `Settings` suffix for the views, which collides with the settings records' names.

### D3. `ToolRef<TSettings, TConfiguration>` carries the view type as well

- **Context:** the spec sketched `ToolRef<TSettings>`. The tool-defaults API needs the typed view of the tool to
  configure, and the view is a generated type the library cannot name.
- **Decision:** `ToolDefaultsManager.ConfigureAsync(Toolset.Tools.X, view => view.Limit = 3)` infers both types from
  the reference. Views implement `IStoredSettingsView<TSelf>` with static abstract `Tool` and `Create`, so no
  reflection creates them. Agent views do the same through `IAgentConfigurationView<TSelf>`.
- **Alternatives:** a generic `ConfigureAsync<TView>` the caller spells out; a generated extension method per tool.

### D4. The agent's `ConfigureAsync` is a C# 14 extension block

- **Context:** `AgentRuntime<TAgent>` is a library type; the typed `ConfigureAsync` needs the generated view type.
- **Decision:** the generator emits `extension(AgentRuntime<TAgent> runtime) { ConfigureAsync(...) }`, matching the
  owner's rule that extension members use extension blocks. Consumers therefore need C# 14, the default of .NET 10.
- **Alternatives:** a classic `this` extension method, which would let C# 13 consumers compile.

### D5. Settings picks and chat-option picks are read only by the generator

- **Context:** `WithSettings<T>(s => s.Configurable(x => x.Limit))` and `WithChatOptions(o, p => p.Configurable(...))`
  name the allowlisted members.
- **Decision:** the generator reads the lambdas from the constructor and emits the allowlist; at runtime the pickers
  are never invoked. The allowlist exists once, in generated code, without expression-tree compilation.
- **Alternatives:** recording the picks at runtime, which duplicates the allowlist and needs expression trees under
  AOT.

### D6. Generic toolsets and agents are not generated

- **Context:** a generic toolset's tools depend on its type arguments, which only the closing site knows.
- **Decision:** discovery skips generic classes. Their misuse surfaces through the runtime guards (a toolset without
  a generated binding throws when asked for a tool), which the runtime-guard tests use on purpose.
- **Alternatives:** generating per closed type found in `UseToolset<T>` calls.

### D7. Toolsets have no stable key besides their tools' names

- **Context:** the spec asked whether toolsets need a key besides the type name.
- **Decision:** no. Stores, catalogs and stored agents address tools by their lower_snake_case name, which is unique
  host-wide (AGT008), so a toolset can move or be renamed without touching stored data. `DisplayName` is for people.
- **Alternatives:** a `WithKey("...")` on the toolset.

### D8. Diagnostics beyond the spec's list

- **Decision:**
  - AGT015 reports a tool name that is not lower_snake_case; the spec's AGT006 only covered agent names.
  - AGT007 uses a free-form message, because it covers two causes (a method of another toolset, a toolset not
    compiled with the generator) whose fixes differ.
  - AGT008 and AGT010 are reported on the first own declaration, or on `AddAgentic()` when the clash is only among
    references.
- **Alternatives:** one ID per AGT007 cause.

### D9. Every project that declares toolsets or agents references the package

- **Context:** NuGet does not flow analyzers to the projects that reference a consumer (`PrivateAssets` defaults
  include `analyzers`).
- **Decision:** document it, and catch the mistake with AGT014. Flowing the analyzer through `buildTransitive` would
  load the generator twice in projects that also reference the package directly.
- **Alternatives:** a `buildTransitive` targets file that adds the analyzer when it is missing; possible later.

## Runtime

### D10. Catalogs are library classes fed by the generated modules

- **Decision:** `ToolCatalog` and `AgentCatalog` live in the library and read the toolsets and agents the modules
  register. Generated code stays small and the catalogs' behavior is tested once.
- **Alternatives:** a generated catalog per host, as the spec sketched.

### D11. Each tool call runs in a DI scope of its own

- **Context:** `FunctionInvokingChatClient` hands tools the root provider.
- **Decision:** `GeneratedToolFunction` creates a scope per call and puts the resolved settings in the scope's
  accessor, so `GetToolSettings<T>()` and scoped services work per call. A container without
  `IServiceScopeFactory` throws, naming `AddAgentic()`.
- **Alternatives:** an `AsyncLocal` for the settings, which is static mutable state.

### D12. Nullable parameters are nullable at the schema root only

- **Context:** `AIJsonUtilities.CreateJsonSchema` does not mark a nullable reference parameter as nullable.
- **Decision:** the generator records the nullability and the library widens the parameter's root `type` to
  `[type, "null"]`. Nested nullability follows Microsoft.Extensions.AI unchanged, and the schema test compares
  against `AIFunctionFactory` for that reason.
- **Alternatives:** rewriting nested schemas, which diverges from what providers already accept.

### D13. Agents resolve through one path, code or stored

- **Decision:** a `StoredAgent` becomes an internal `StoredAgentDefinition : AgentDefinition`, so stored agents use
  the same `CreateChatOptions` as code agents. `StoredAgentOptions` holds the chat options a person sets on a stored
  agent (instructions, sampling, penalties, seed, stop sequences, `AllowMultipleToolCalls`), all of them allowlisted.
- **Alternatives:** storing a full `ChatOptions`, most of which (tools, response format, raw factories) cannot be
  data.

### D14. Stored agents get generated names that cannot clash with code agents

- **Decision:** `StoredAgent.Create(displayName, time)` names the agent `stored-{guid v7}`. The hyphen can never
  appear in a lower_snake_case code agent name. `SaveAsync` also rejects a stored agent named like a code agent.
- **Alternatives:** a slug of the display name, which can clash and changes when people rename.

### D15. Listing and exporting agents

- **Decision:** `AgentCatalog.ListAsync` shows a code agent's unique name as its display name (code agents have no
  display name). `Export` keeps the code agent's unique name; saving the export as a stored agent therefore needs a
  new name, which keeps code agents authoritative.
- **Alternatives:** an optional `WithDisplayName` on agents.

### D16. Stores are optional singletons, tool defaults load in batches

- **Decision:** every store interface is optional; without one the runtime works from code and updates throw, naming
  the store to register. The catalogs and managers are singletons, so stores must be singletons too.
  `IToolDefaultsStore.LoadAsync` takes all tool names of a run at once, so resolving an agent costs one round trip.
- **Alternatives:** scoped stores behind a factory; one load per tool.

### D17. The model provider interface

- **Decision:** `IChatClientProvider.GetChatClientAsync(string model, ct)` maps the configured model name to a client.
  `AgentRuntime.CreateChatClientAsync` wraps it: conversation tracking outside, `FunctionInvokingChatClient` inside.
  A missing model throws `ConfigurationRequiredException`.
- **Alternatives:** keyed `IChatClient` registrations, which cannot depend on stored configuration.

## Conversations

### D18. The port of the owner's conversation tracking

- **Decision:**
  - `TrackedConversationManager` became `ConversationTracker`; the initialize and resume option classes became one
    internal `ConversationStart` record; `ProviderModel` became the plain model name the provider maps.
  - Options reach the inner client unchanged, as in the owner's code.
  - Storing a finished session does not observe the call's token, so cancelled calls are stored too.
  - Resuming drops function calls that never got a result (a call cancelled mid-tool), which providers reject.
  - Subscribers are awaited until their limit token fires instead of on a second timer. The behavior is the same, and
    tests that advance a `FakeTimeProvider` cannot race the wait.
- **Alternatives:** keeping the option classes public, which adds API the alpha does not need.

## Repository

### D19. Sealing

- **Decision:** generated classes, assertion classes and classes whose inheritance would be a bug (`AgentRuntime<T>`,
  the catalogs, the in-memory stores) are sealed; the rest stays open, as the owner's code does.

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
