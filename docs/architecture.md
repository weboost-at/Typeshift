# Architecture

Agentic is one package with two assemblies: the runtime library `Agentic` and the Roslyn generator
`Agentic.Generators`, which the package ships as an analyzer. The developer writes toolsets and agents as ordinary
classes; the generator reads them at compile time and writes everything that would otherwise need reflection.

```text
developer code           generator, at compile time                   runtime
--------------           --------------------------                   -------
partial Toolset   --->   toolset model --> AIFunction subclasses ---> ToolCatalog
                                       --> settings binder, views
partial Agent     --->   agent model, resolved against every     ---> AgentCatalog,
                         toolset --> Name, allowlist, view            AgentRuntime<TAgent>
the assembly      --->   module class + [assembly: GeneratedModule] stamp
AddAgentic() call --->   interceptor: core services + own module + every referenced module
```

## The runtime library

A namespace follows its folder.

| Folder | Holds |
|---|---|
| `Tools/` | `ToolName`, `ToolRef` and `ToolRef<TSettings, TConfiguration>`, `ToolParameter`, the `GeneratedToolFunction` base every generated tool derives from, and `GetToolSettings<T>()` on `IServiceProvider`. |
| `Toolsets/` | `Toolset`, `ToolDefinition` and the `AddTool(...)` builder. |
| `Settings/` | `StoredSettings` (the JSON document of stored values), the typed views over it, `ToolSettingsBinder<T>` (the layering), `ToolDefaultsManager`. |
| `Agents/` | `AgentDefinition`, `AgentRuntime` and `AgentRuntime<TAgent>`, `AgentConfiguration` and its typed view, `RunOverrides`, `ChatExecutionContext`, `IChatClientProvider`. |
| `StoredAgents/` | `StoredAgent`, the record an agent defined as data is stored as, and the internal definition that runs it. |
| `Catalogs/` | `ToolCatalog` and `AgentCatalog`, the registries the generated modules fill. |
| `Conversations/` | The conversation tracking: `TrackedChatClient`, `ConversationTracker`, the sessions and `StoredConversation`. |
| `Persistence/` | The store interfaces and their in-memory implementations. |
| `Json/` | `AgentJsonOptions`, `AgentJson` (the serializer options and schema cache), the library's `AgentJsonContext`. |
| `Modules/` | The stamps (attributes) and `ModuleRegistration`, which generated code calls. Hidden from editors. |
| `DependencyInjection/` | `AddAgentic()`, which throws unless the generator intercepted the call. |

## The generator

`AgentGenerator` is an `IIncrementalGenerator`. Its pipeline steps are named (`TrackingNames`) and a test proves that
an unrelated edit leaves every output cached.

1. **Discovery** (`Parsing/Discovery.cs`) is attribute-free. The syntax predicate takes any class declaration with a
   base list that is not abstract; the transform checks semantically whether it derives from `Toolset` or
   `AgentDefinition`. Generic classes are left alone: their tools cannot be generated without knowing the type
   arguments, and the runtime guards cover them.
2. **Parsing** turns symbols into models. `ConstructorChains` reads the constructor through `IOperation`: every
   `AddTool(...).WithName(...).WithDescription(...).WithSettings<T>(...)` chain, and in agents every
   `WithUniqueName`, `WithChatOptions`, `UseToolset` and `WithToolSettings` call. The settings picks
   (`Configurable(s => s.Limit)`) are read here too; at runtime the pickers do nothing.
3. **Models** hold only strings, bools and `EquatableArray<T>`; locations are `LocationInfo` and diagnostics
   `DiagnosticInfo`, both value types. An agent model holds the toolset's type name and the selected method names,
   never the toolset's syntax.
4. **Resolution** combines each agent with the summaries of all toolsets (own and referenced) in a separate step, so
   a change to a toolset's tools regenerates the agents that use them. The resolved agents and the module carry no
   locations, so code that only moves leaves them cached.
5. **References** are read from metadata stamps only: `[assembly: GeneratedModule(typeof(...))]` names a referenced
   assembly's module, `[ModuleToolset]` and `[ModuleAgent]` on the module list its toolsets and agents, and
   `[GeneratedTool]` on a toolset lists its tools with their method, settings type and configurable members.
6. **Emitting** (`Emitting/`) writes the code from the models with a small `SourceWriter`.

What it writes:

| For | Generated |
|---|---|
| each toolset | The stamps; a `Tools` class of typed `ToolRef`s; per tool with settings a `{Tool}Configuration` view over its stored values; the `GetBinding` switch; per tool a binding and a sealed `AIFunction` subclass. |
| each agent | The `Name` constant, `ToolNames`, the `ConfigurableChatOptions` allowlist, the `Configuration` view, and a `ConfigureAsync` extension on `AgentRuntime<TAgent>`. |
| each assembly | `Agentic.Generated.{Assembly}Module`, which registers the assembly's toolsets and agents with factory lambdas, plus its assembly stamp. |
| each `AddAgentic()` call | One interceptor that registers the core services, the own module and every referenced module. |

## Registration

`services.AddAgentic()` is the library's one public method, and it throws. In every project that calls it, the
generator emits a `file static class` in `Agentic.Generated` with an `[InterceptsLocation]` for each call, so the
compiler binds the call to the generated method instead. The project opts into the namespace through the
`InterceptorsNamespaces` property, which the package sets in `buildTransitive/Agentic.props`.

The interceptor calls `ModuleRegistration.AddCoreServices` and then `Register` on each module. A module registers
itself once per service collection, so calling `AddAgentic()` twice, or from two assemblies, adds nothing twice.
Toolsets and agents are singletons, created by factory lambdas; agents are also registered as `AgentDefinition` and
get an `AgentRuntime<TAgent>`. The catalogs collect them from the container.

## Tool invocation

The generated function derives from `GeneratedToolFunction`, which derives from `AIFunction`.

1. `FunctionInvokingChatClient` calls `InvokeAsync` with the model's arguments and the root service provider.
2. `GeneratedToolFunction.InvokeCoreAsync` creates a DI scope for the call and puts the tool's resolved settings in the
   scope's settings accessor, so `services.GetToolSettings<T>()` returns them and scoped services are per call.
3. The generated `InvokeToolAsync` binds each parameter with `GetArgument<T>`: values that are already `T`, JSON
   elements and JSON text are converted through `JsonTypeInfo<T>`; a missing required argument throws an
   `ArgumentException`, an absent optional one takes its default. Services, the token and the raw arguments are
   passed through.
4. The result is serialized through `JsonTypeInfo` into a `JsonElement`, which reaches the model as JSON.

The schema of each function type is built once and cached in `AgentJson`: one `AIJsonUtilities.CreateJsonSchema` per
parameter with its description and default, the nullable ones widened to `[type, "null"]`, then the schema transform
options applied to the whole.

## Settings layering

A tool with settings resolves them per call from five layers, lowest first:

1. the code defaults, the settings record's parameterless constructor;
2. the stored tool defaults (`IToolDefaultsStore`), skipped when the agent sets the tool's settings in code;
3. the agent's code settings (`WithToolSettings`), applied as a whole object;
4. the agent's stored overrides (`AgentConfiguration.ToolSettings`);
5. the run's overrides (`RunOverrides.ToolSettings`).

Layers 2, 4 and 5 change only the members the tool marks `Configurable(...)`, and a stored null inherits from the
layer below. Chat options layer the same way: the agent's code options, its stored overrides, then the run's, the last
two limited to the agent's allowlist. The conversation id comes from the run; the execution context is always the
agent's unique name.

Stored values are `StoredSettings`, a dictionary of member names to JSON. The generated views read and write them
with typed properties, so a member that is not allowlisted has no property: setting it is a compile error.

## Agents at runtime

`AgentRuntime` wraps one `AgentDefinition` with the services it needs:

- `LoadConfigurationAsync` and `UpdateConfigurationAsync` read and write its `AgentConfiguration`
  (`IAgentConfigurationStore`); the generated `ConfigureAsync` does the same through the typed view.
- `ResolveChatOptionsAsync(run)` loads the configuration and the tool defaults in one batch and builds the
  `ChatOptions` of a run: the tools of the agent's own chat options first (a hosted web search, say), then those of
  its toolsets. Code agents and stored agents share this one path.
- `CreateChatClientAsync` asks the `IChatClientProvider` for the configured model's client and wraps it in
  conversation tracking around a `FunctionInvokingChatClient`.

A stored agent is a `StoredAgent` record (`IAgentDefinitionStore`). The catalog turns it into an internal
`AgentDefinition` that looks its tools up in the `ToolCatalog` and imports its settings, so it runs exactly like a
code agent. `AgentCatalog.Export` turns a code agent into such a record.

Every store is optional. Without one, the runtime works from the code alone and an update throws, naming the store to
register.

## Conversations

`TrackedChatClient` sits outside the function loop, so one user turn with all its tool calls is one session.

- `ChatOptions.ConversationId` names the conversation. A stored one is resumed: its earlier messages are sent ahead
  of the new input, minus function calls that never got a result. An id that is not stored starts a new conversation
  under that id. The provider never receives the id, since the tracker resends the conversation itself.
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

All serialization goes through `JsonTypeInfo`. `AgentJson.SerializerOptions` is a copy of
`AIJsonUtilities.DefaultOptions` whose resolver chain is: the developer's resolvers from
`AgentJsonOptions.TypeInfoResolverChain`, then the library's `AgentJsonContext`, then the defaults of
Microsoft.Extensions.AI. The generated code never reflects over user types, so a host that registers a
source-generated context for its own parameter and result types publishes with zero trim and AOT warnings; CI proves
it with the console sample.
