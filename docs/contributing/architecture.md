# Architecture

Typeshift is one package with two assemblies: the runtime library `Typeshift` and `Typeshift.Generators`, which holds
the source generator, the analyzer and a diagnostic suppressor and ships as an analyzer. The developer writes
toolsets (and, from slice 2, agents) as ordinary classes; the generator reads them at compile time and writes
everything that would otherwise need reflection. The decisions behind each part are in [decisions](./decisions.md),
cited here as D-numbers.

```text
developer code         compile time                                         runtime
--------------         ------------                                         -------
partial Toolset  --->  ToolsetParser --> ToolsetModel --> ToolsetEmitter -> {Tool}Overrides, Create{Tool}(),
                         |                                                  a GeneratedToolFunction per tool,
                         |                                                  a ToolList<T> extension per toolset
                         +--> TypeshiftAnalyzer: TSH diagnostics at real locations
```

## The runtime library

| Namespace | Holds |
|---|---|
| `Typeshift` | `Toolset` and the tool chain (`ToolBuilder`, the settings builders), `ToolName`, `ToolList<T>`. |
| `Typeshift.Generated` | What only generated code calls, hidden from editors (D10): `GeneratedToolFunction`, `ToolParameter`, `ToolJson`. |

At runtime the declaration chains do nothing but return the next builder: the generated code is the one truth about
which tools exist and how they are configured (D4).

## The generator and the analyzer

`TypeshiftGenerator` is an `IIncrementalGenerator`; `TypeshiftAnalyzer` a `DiagnosticAnalyzer`. Both parse with the
same code: the analyzer passes a `Reporter` that reports, the generator `Reporter.None`, and skips what does not
parse (D2).

1. **Discovery** (`Parsing/Discovery.cs`) is attribute-free: the syntax predicate passes classes whose base list names
   `Toolset`, the semantic check confirms the direct base (D3).
2. **Parsing** (`Parsing/ToolsetParser.cs`) reads the constructors through `IOperation`: each
   `AddTool(...).WithName(...).ReadOnly().WithSettings<T>().Configurable(...)` chain, only directly in a constructor
   and only as one statement (D4). The analyzer receives the constructors as operation blocks, the generator reads
   them through the semantic model.
3. **Models** hold only strings, bools, enums and `EquatableArray<T>`; no locations, so code that moves within a
   toolset leaves its output cached.
4. **Emitting** (`Emitting/ToolsetEmitter.cs`) writes per tool an overrides record when it has configurable settings,
   a hidden `Create{Tool}` factory and a private `GeneratedToolFunction` subclass; per toolset a `{Toolset}ToolList`
   extension block with one property per tool.

The analyzer also checks what spans declarations (unique tool names) in a compilation-end action, and calls the
generator cannot read (TSH014, TSH015) in an operation action. `ParameterDocsSuppressor` suppresses CS1573 for the
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
