# Architecture

Typeshift is one package with two assemblies: the runtime library `Typeshift` and `Typeshift.Generators`, which holds
the source generator, the analyzer and a diagnostic suppressor and ships as an analyzer. The developer writes
toolsets and agents as ordinary classes; the generator reads them at compile time and writes everything that would
otherwise need reflection. The decisions behind each part are in [decisions](./decisions.md),
cited here as D-numbers.

```text
developer code         compile time                                         runtime
--------------         ------------                                         -------
partial Toolset  --->  ToolsetParser --> ToolsetModel --> ToolsetEmitter -> {Tool}Overrides, Create{Tool}(),
                         |                   |                              a GeneratedToolFunction per tool,
                         |                   v                              a ToolList<T> extension, stamps
                         |              Catalog (+ the references' stamps)
                         |                   |
partial Agent    --->  AgentParser ---> AgentModel --> AgentEmitter ------> Settings, Configure{Tool}(), the
                         |                                                  declaration, AgentHost<T> members
services.AddTypeshift() --> InterceptParser --> InterceptorEmitter -------> AddTypeshift(): the modules
                         |                     ModuleEmitter -------------> a module per assembly
                         +--> TypeshiftAnalyzer: TSH diagnostics at real locations
```

## The runtime library

| Namespace | Holds |
|---|---|
| `Typeshift` | `Toolset` and the tool chain (`ToolBuilder`, the settings builders), `ToolName`, `ToolList<T>`; `Agent` and its chains (`ChatOptionsBuilder`, `ToolsetUse<T>`, `RunOptionsBuilder`), `RunOptions`; `AgentHost<T>`, `IChatClientProvider`, `TypeshiftBuilder`. |
| `Typeshift.Generated` | What only generated code calls, hidden from editors (D10): `GeneratedToolFunction`, `ToolParameter`, `ToolJson`, `AgentDeclaration`, `AgentRuns`, `ModuleRegistration` and the stamps. |
| `Microsoft.Extensions.DependencyInjection` | `AddTypeshift()`, which throws unless the generator intercepts it (D1). |

At runtime the declaration chains do nothing but return the next builder, or record a value (`WithDisplayName`, the
chat and run options, `Configure{Tool}()`): the generated code is the one truth about which tools exist, what an
agent offers and what may change at runtime (D4).

## The generator and the analyzer

`TypeshiftGenerator` is an `IIncrementalGenerator`; `TypeshiftAnalyzer` a `DiagnosticAnalyzer`. Both parse with the
same code: the analyzer passes a `Reporter` that reports, the generator `Reporter.None`, and skips what does not
parse (D2).

1. **Discovery** (`Parsing/Discovery.cs`) is attribute-free: the syntax predicate passes classes whose base list names
   `Toolset`, `Agent` or `JsonSerializerContext`, the semantic check confirms the direct base (D3).
2. **Parsing** (`Parsing/ToolsetParser.cs`) reads the constructors through `IOperation`: each
   `AddTool(...).WithName(...).ReadOnly().WithSettings<T>().Configurable(...)` chain, only directly in a constructor
   and only as one statement (D4). The analyzer receives the constructors as operation blocks, the generator reads
   them through the semantic model.
3. **Models** hold only strings, bools, enums and `EquatableArray<T>`; no locations, so code that moves within a
   toolset leaves its output cached.
4. **Emitting** (`Emitting/ToolsetEmitter.cs`) writes per tool an overrides record when it has configurable settings,
   a hidden `Create{Tool}` factory and a private `GeneratedToolFunction` subclass; per toolset a `{Toolset}ToolList`
   extension block with one property per tool, and a `[GeneratedTool]` stamp per tool for agents in other assemblies
   (D7).
5. **Agents** (`Parsing/AgentParser.cs`) read the chat and run option picks, the toolset uses and their picks. A pick
   from a tool list does not bind in the generator, whose own output the list's properties are, so the parser walks
   those calls as invalid operations and reads the picks from syntax. `Parsing/Catalog.cs` resolves the uses against
   the toolsets of the compilation and those the references stamp (`Parsing/ReferenceReader.cs`); an agent's output
   is cached while what it offers stays the same.
6. **Registration**: `Emitting/ModuleEmitter.cs` writes one module per assembly that declares toolsets or agents,
   stamped with `[GeneratedModule]`; `Emitting/InterceptorEmitter.cs` intercepts every `AddTypeshift()` call and
   registers the core services, the project's module and its references' modules (D1).

The analyzer also checks what spans declarations (unique tool names, the references' stamps included; references
without a module) in a compilation-end action, and calls the generator cannot read (TSH014, TSH015) in an operation
action. `ParameterDocsSuppressor` suppresses CS1573 for the
parameters of a tool the model never sees (D14).

## Tool invocation

1. `FunctionInvokingChatClient` calls `InvokeAsync` with the model's arguments and the services it was built with:
   the run's (D25).
2. The generated `InvokeToolAsync` binds each parameter with `GetArgument<T>`: values that are already `T`, JSON
   elements and JSON text are converted through `JsonTypeInfo<T>`; a missing required argument throws an
   `ArgumentException`, an absent optional one takes its default. The settings the function was created with, the
   services, the token and the raw arguments are passed through and are not in the schema (D16).
3. The result is serialized through `JsonTypeInfo` into a `JsonElement`.

The schema of each function type is built once and cached in `ToolJson`: one `AIJsonUtilities.CreateJsonSchema` per
parameter with its description and default, nullable references widened to `[type, "null"]`. `ToolJson` asks the
modules' JSON contexts first, then Microsoft.Extensions.AI's defaults, which reflect outside Native AOT (D45).

## A run

1. The generated `RunAsync` passes the input, the settings id (boxed) and the run's `Settings` to `AgentRuns`, which
   calls the host.
2. `AgentHost<T>` resolves the options: a copy of the agent's chat options, its instructions, then each settings
   layer (the stored settings, loaded through the declaration, then the run's) through the generated declaration
   (configurable options, run options), then the offered tools, created with their layered settings (D27).
3. It asks the scope's `IChatClientProvider` for the model's client and builds the pipeline, outermost first: author
   names on every message (D26), `FunctionInvokingChatClient` with the run options and the scope's services, and the
   provider's client behind a wrapper that never disposes it (D29).
4. The options are a `ChatOptions` subclass that remembers the run options, so the raw path's client gets them too.

## Storage

`ITypeshiftStore<TId>` keeps documents by kind and id, with revisions (D38, D41). The library never sees the id type:
the interceptor registers `StoreAccess<TId>` through `AddCoreServices<TId>`, which the runs reach as `IStoreAccess`
with boxed ids (D11). The generator writes each agent's `Settings` as an `IStorable<Settings>` (kind, version,
`Serialize`, `Deserialize`) and an `IJsonStorable<Settings>` (`WriteJson`, `TryReadJson`, used by `Serialize` and by
the `StorableJsonConverter` that lets `System.Text.Json` speak the stored shape). Tool overrides are
`IJsonStorable` too, so an agent's settings delegate to the overrides of a toolset in another assembly (D44).
