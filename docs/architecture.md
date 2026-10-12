# Architecture

Agentic is one package with two assemblies: the runtime library `Agentic` and the Roslyn generator
`Agentic.Generators`, which the package ships as an analyzer. The developer writes toolsets and agents as ordinary
classes; the generator reads them at compile time and writes everything that would otherwise need reflection. The
decisions behind each part are in `docs/decisions.md`, cited here as D-numbers.

```text
developer code           generator, at compile time                   runtime
--------------           --------------------------                   -------
partial Toolset   --->   toolset model --> AIFunction subclasses ---> ToolCatalog, agents
                                       --> {Tool}Overrides, selection methods
partial Agent     --->   agent model, resolved against every     ---> AgentRuntime<TAgent>
                         toolset --> Settings, WithToolSettings,      + generated typed members
                         runtime extension members
the assembly      --->   module class + [assembly: GeneratedModule] stamp
AddAgentic() call --->   interceptor: core services + own module + every referenced module
```

## The runtime library

A namespace follows its folder.

| Folder | Holds |
|---|---|
| `Tools/` | `ToolName`, `ToolRef` and `ToolRef<TSettings, TOverrides>`, `ToolSelection<T>`, `ToolParameter`, the `GeneratedToolFunction` base every generated tool derives from, the bindings, and `GetToolSettings<T>()`. |
| `Toolsets/` | `Toolset`, `ToolDefinition` and the `AddTool(...)` builder. |
| `Settings/` | `IToolOverrides`, the layering (`ToolSettingsLayers`), `ToolDefaults` and the settings picker. |
| `Agents/` | `AgentDefinition`, `AgentRuntime` and `AgentRuntime<TAgent>`, `IAgentSettings`, `ChatExecutionContext`, `IChatClientProvider`. |
| `Catalogs/` | `ToolCatalog`, every tool the host can see, created by name. |
| `Conversations/` | The conversation tracking: `TrackedChatClient`, `ConversationTracker`, the sessions, `StoredConversation`, and the bridge that parses conversation ids into the store's id type. |
| `Persistence/` | `IAgenticStore<TId>`, `IStorable<TSelf>`, the `StorableJson` helpers generated code serializes with, and `InMemoryAgenticStore<TId>`. |
| `Json/` | `AgentJsonOptions`, `AgentJson` (the serializer options and schema cache), the library's `AgentJsonContext`. |
| `Modules/` | The stamps (attributes) and `ModuleRegistration`, which generated code calls. Hidden from editors. |
| `DependencyInjection/` | `AddAgentic()`, which throws unless the generator intercepted the call, and `AddAgenticStore()`. |

## The generator

`AgentGenerator` is an `IIncrementalGenerator`. Its pipeline steps are named (`TrackingNames`) and a test proves that
an unrelated edit leaves every output cached.

1. **Discovery** (`Parsing/Discovery.cs`) is attribute-free. The syntax predicate takes any class declaration with a
   base list that is not abstract; the transform checks semantically whether it derives from `Toolset` or
   `AgentDefinition`. Generic classes are left alone (D6).
2. **Parsing** turns symbols into models. `ConstructorChains` reads the constructor through `IOperation`: every
   `AddTool(...).WithName(...).WithDescription(...).WithSettings<T>(...)` chain, and in agents `WithChatOptions`
   and the configurable picks. `UseToolset<T>(tools => tools.A().B())` does not bind in the generator's own
   compilation, because the selection methods are what it generates, so `AgentParser` reads it from syntax and the
   overload's candidate symbols (D29). `Storage` decides per configurable member how the records write and read it,
   or reports AGT014.
3. **Models** hold only strings, bools and `EquatableArray<T>`; locations are `LocationInfo` and diagnostics
   `DiagnosticInfo`, both value types. An agent model holds the toolset's type name and the selected tool names,
   never the toolset's syntax.
4. **Resolution** (`AgentResolver`) combines each agent with the summaries of all toolsets (own and referenced) in a
   separate step, so a change to a toolset's tools regenerates the agents that use them. The resolved agents and the
   module carry no locations, so code that only moves leaves them cached.
5. **References** are read from metadata stamps only: `[assembly: GeneratedModule(typeof(...))]` names a referenced
   assembly's module, `[ModuleToolset]` and `[ModuleAgent]` on the module list its toolsets and agents, and
   `[GeneratedTool]` on a toolset lists its tools with their settings type and configurable members.
6. **Emitting** (`Emitting/`) writes the code from the models with a small `SourceWriter`; `StorableEmitter` writes
   the `Serialize` and `Deserialize` members both kinds of record share, `Docs` wraps the XML docs.

What it writes:

| For | Generated |
|---|---|
| each toolset | The stamps; a `Tools` class of typed `ToolRef`s; per tool with configurable settings a `{Tool}Overrides` record; the `GetBinding` switch; per tool a binding and a sealed `AIFunction` subclass; a `{Toolset}Selection` extension block with one selection method per tool. |
| each agent | Per settings type its tools read a private `WithToolSettings` overload and field, the `GetToolSettings` override, the nested `Settings` record, and a `{Agent}RuntimeExtensions` extension block on `AgentRuntime<TAgent>`. |
| each assembly | `Agentic.Generated.{Assembly}Module`, which registers the assembly's toolsets and agents with factory lambdas, plus its assembly stamp. |
| each `AddAgentic()` call | One interceptor that registers the core services, the own module and every referenced module. |

## Registration

`services.AddAgentic()` is a library method that throws (D1). In every project that calls it, the generator emits a
`file static class` in `Agentic.Generated` with an `[InterceptsLocation]` for each call, so the compiler binds the
call to the generated method instead. The project opts into the namespace through the `InterceptorsNamespaces`
property, which the package sets in `buildTransitive/Agentic.props`.

The interceptor calls `ModuleRegistration.AddCoreServices` and then `Register` on each module. A module registers
itself once per service collection, so calling `AddAgentic()` twice, or from two assemblies, adds nothing twice.
Toolsets and agents are singletons, created by factory lambdas; each agent also gets an `AgentRuntime<TAgent>`,
registered as `AgentRuntime` too, so a host can list them.

`services.AddAgenticStore<TStore, TId>()` registers the one store (D25, D26).

## Tool invocation

The generated function derives from `GeneratedToolFunction`, which derives from `AIFunction`.

1. `FunctionInvokingChatClient` calls `InvokeAsync` with the model's arguments and the root service provider.
2. `GeneratedToolFunction.InvokeCoreAsync` creates a DI scope for the call and puts the tool's resolved settings in the
   scope's settings accessor, so `services.GetToolSettings<T>()` returns them and scoped services are per call (D11).
3. The generated `InvokeToolAsync` binds each parameter with `GetArgument<T>`: values that are already `T`, JSON
   elements and JSON text are converted through `JsonTypeInfo<T>`; a missing required argument throws an
   `ArgumentException`, an absent optional one takes its default. Services, the token and the raw arguments are
   passed through.
4. The result is serialized through `JsonTypeInfo` into a `JsonElement`, which reaches the model as JSON.

The schema of each function type is built once and cached in `AgentJson`: one `AIJsonUtilities.CreateJsonSchema` per
parameter with its description and default, the nullable ones widened to `[type, "null"]` (D12), then the schema
transform options applied to the whole.

## Settings layering

A tool with settings resolves them per run from five layers, lowest first:

1. the code defaults, the settings record's parameterless constructor;
2. the stored tool defaults, the tool's `{Tool}Overrides` stored under the run's id, skipped when the agent sets the
   tool's settings in code;
3. the agent's code settings (`WithToolSettings`), applied as a whole object;
4. the agent's stored settings, its `Settings` stored under the id, through their `{Tool}Overrides` property;
5. the run's `Settings`.

Layers 2, 4 and 5 are `IToolOverrides<TSettings>`: their `ApplyTo` returns a `with` copy in which every non-null
member replaces the one below. Only configurable members exist on them, so a member that is not configurable cannot
be set at all. Chat options layer the same way through `IAgentSettings.ApplyTo`: the agent's code options, its stored
`Settings`, then the run's; `Model` sets `ModelId` (D34). The conversation id comes from the run; the execution
context is always the agent's name.

## Agents at runtime

`AgentRuntime` wraps one `AgentDefinition` with the services it needs. Its typed members are generated per agent
(D4) and call hidden methods of the library:

- `ResolveChatOptions(run, conversationId)` builds a run's `ChatOptions` from the code alone; it needs no store.
- `ResolveChatOptionsAsync(id, run, conversationId)` loads the agent's `Settings` and its tools' defaults under the id
  (D28), then builds the options. Both put the tools of the agent's own chat options first (a hosted web search,
  say), then those of its toolsets.
- `LoadSettingsAsync`, `SaveSettingsAsync` and `ConfigureAsync` read and write the agent's `Settings` under an id.
- `CreateChatClientAsync(options)` asks the `IChatClientProvider` for the client of `options.ModelId` and wraps it in
  conversation tracking around a `FunctionInvokingChatClient` (D17).

`ToolDefaults` reads and writes a tool's stored defaults, typed through the tool's `ToolRef` (D3).

## Persistence

Everything the library persists is an `IStorable<TSelf>`: the generated `Settings` and `{Tool}Overrides` records, and
`StoredConversation`. `IAgenticStore<TId>` loads and saves them by type and id (D25). The generated records serialize
member by member through `StorableJson`, without a `JsonSerializerContext` (D27); `StoredConversation` goes through
the library's own `AgentJsonContext`.

Without a store, every API that loads or saves throws, naming `AddAgenticStore<TStore, TId>()` (D26); the rest works
from code.

## Conversations

`TrackedChatClient` sits outside the function loop, so one user turn with all its tool calls is one session.

- `ChatOptions.ConversationId` names the conversation. When it parses as the store's `TId` (D33), a stored
  conversation is resumed: its earlier messages are sent ahead of the new input, minus function calls that never got
  a result. An id that is not stored starts a new conversation under that id. A call without an id, without a store,
  or with an id that does not parse is tracked live and not stored. The provider never receives the id, since the
  tracker resends the conversation itself (D18).
- When the call ends, successfully or not, the session is stored: the new input and every message the model produced,
  its usage and finish reason. A failed call is stored with an `ErrorContent` and rethrown; a stream that fails, is
  cancelled or is left early is stored with the updates that arrived. Storing does not observe the call's
  cancellation, so a cancelled call is stored as well. When two first sessions of a new conversation overlap, the one
  that ends second appends to the conversation the first created.
- `ConversationTracker` lists the running sessions and raises `OnConversationsChanged`. A streaming session
  (`RealtimeTrackedConversationSession`) also publishes its updates: anything but text, reasoning or usage at once,
  text at most every 100 ms. Every subscriber gets a token cancelled at its time limit, the call waits no longer, and
  what a subscriber throws stays with it.

## JSON and Native AOT

All serialization goes through `JsonTypeInfo` or `Utf8JsonWriter`. `AgentJson.SerializerOptions` is a copy of
`AIJsonUtilities.DefaultOptions` whose resolver chain is: the developer's resolvers from
`AgentJsonOptions.TypeInfoResolverChain`, then the library's `AgentJsonContext`, then the defaults of
Microsoft.Extensions.AI. The generated code never reflects over user types, so a host that registers a
source-generated context for its own parameter and result types publishes with zero trim and AOT warnings; CI proves
it with the console sample.
