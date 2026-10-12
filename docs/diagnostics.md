# Diagnostics

The generator reports these diagnostics while it reads toolsets, agents and the `AddAgentic()` call. They are tracked
in `src/Agentic.Generators/AnalyzerReleases.Unshipped.md`, declared in
`src/Agentic.Generators/Diagnostics/Descriptors.cs`, and each one has its own test in
`tests/Agentic.Generators.Tests/Diagnostics/DiagnosticTests.cs`.

Runtime guards stay for what the generator cannot see: stored agents, generic toolsets and agents, and calls made
through a delegate. Each guard throws an exception whose message says what to change.

| ID | Severity | Title |
|---|---|---|
| [AGT001](#agt001) | Error | Toolset or agent is not partial |
| [AGT002](#agt002) | Error | Tool name or description is not a constant |
| [AGT003](#agt003) | Error | Tool has no name |
| [AGT004](#agt004) | Error | Tool is not a static method group |
| [AGT005](#agt005) | Error | Settings type is not a record |
| [AGT006](#agt006) | Error | Agent has no valid unique name |
| [AGT007](#agt007) | Error | Selected tool cannot be resolved |
| [AGT008](#agt008) | Error | Tool name is not unique |
| [AGT009](#agt009) | Warning | Tool has no description |
| [AGT010](#agt010) | Error | Agent unique name is not unique |
| [AGT011](#agt011) | Error | Tool takes its settings as a parameter |
| [AGT012](#agt012) | Error | Tool settings are read by no tool |
| [AGT013](#agt013) | Warning | Tool relies on XML docs that are not generated |
| [AGT014](#agt014) | Warning | Referenced assembly was not compiled with the generator |
| [AGT015](#agt015) | Error | Tool name is not lower_snake_case |
| [AGT016](#agt016) | Error | Settings type is less accessible than its toolset |

## AGT001

**Toolset or agent is not partial.** Reported on the class name.

The generator adds members to every toolset and agent (the tool bindings, the `Name` constant, the configuration
view), so the class must be `partial`.

```csharp
public class CompanyToolset : Toolset          // AGT001
public partial class CompanyToolset : Toolset  // fixed
```

## AGT002

**Tool name or description is not a constant.** Reported on the argument.

`WithName(...)` and `WithDescription(...)` are read at compile time: they become the generated function's name and
description and the metadata stamps other assemblies read. Pass a string literal or a `const`.

```csharp
AddTool(FindCompanies).WithName(prefix + "find");  // AGT002
AddTool(FindCompanies).WithName("find_companies"); // fixed
```

## AGT003

**Tool has no name.** Reported on the `AddTool(...)` call.

Every tool needs `WithName(...)`; the name is the tool's identity across stores, agents and assemblies, so it never
comes from the method name.

## AGT004

**Tool is not a static method group.** Reported on the argument of `AddTool`.

A tool is a static method, passed as a method group. Lambdas, instance methods and delegate variables cannot be
generated without reflection.

```csharp
AddTool(query => Find(query));  // AGT004
AddTool(Find);                  // fixed, Find is static
```

## AGT005

**Settings type is not a record.** Reported on `WithSettings<T>`.

Stored values and run overrides apply to a `with` copy of the settings, so the type must be a record. The code
defaults come from its parameterless constructor, which it therefore needs as well.

## AGT006

**Agent has no valid unique name.** Reported on the class name when `WithUniqueName(...)` is missing, or on its
argument when that is not a lower_snake_case constant.

The unique name keys the agent's stored configuration and its conversations' execution context. At runtime,
`WithUniqueName` throws an `ArgumentException` for a name that is not lower_snake_case.

## AGT007

**Selected tool cannot be resolved.** Reported on the offending argument of `UseToolset<T>(...)`, or on the call when
`T` itself was not compiled with the generator. The message says which case applies.

`UseToolset<T>(T.Method, ...)` selects single tools by their method; a method that is not a tool of `T` cannot be
offered. At runtime the same call throws an `ArgumentException`.

```csharp
UseToolset<CompanyToolset>(WebToolset.Search);         // AGT007, Search is not a tool of CompanyToolset
UseToolset<CompanyToolset>(CompanyToolset.FindCompanies); // fixed
```

## AGT008

**Tool name is not unique.** Reported on the first own tool that uses the name, or on the `AddAgentic()` call when the
clash is only among referenced assemblies.

Tool names are global: the catalogs, the stores and the model all address a tool by name. Two tools with one name,
anywhere the host can see, are an error.

## AGT009

**Tool has no description.** A warning, reported on the `AddTool(...)` call.

The model chooses tools by their descriptions. Add `WithDescription(...)` or an XML `<summary>` on the method.

## AGT010

**Agent unique name is not unique.** Reported on the first agent that uses the name, or on `AddAgentic()` when the
clash is only among referenced assemblies. The agent catalog throws an `InvalidOperationException` for a clash it
meets at runtime.

## AGT011

**Tool takes its settings as a parameter.** Reported on the parameter.

A parameter becomes part of the model-facing schema, so a settings parameter would let the model choose the settings.
Read them inside the tool instead:

```csharp
public static Company[] Find(string city, FindCompaniesSettings settings)          // AGT011
public static Company[] Find(string city, IServiceProvider services)               // fixed
{
    var settings = services.GetToolSettings<FindCompaniesSettings>();
    ...
}
```

## AGT012

**Tool settings are read by no tool.** Reported on the argument of `WithToolSettings(...)`.

`WithToolSettings(x)` applies to the tools the agent added before it. If none of them reads the type of `x`, the
settings would never apply. At runtime the same call throws an `ArgumentException`.

## AGT013

**Tool relies on XML docs that are not generated.** A warning, reported on the `AddTool(...)` call.

A tool takes its description from its method's `<summary>` when it has no `WithDescription(...)`, and its parameter
descriptions from `<param>` docs. The compiler only reads them with
`<GenerateDocumentationFile>true</GenerateDocumentationFile>`, so without it they are lost. Turn it on; or, when only
the summary is at stake, describe the tool with `WithDescription(...)`.

## AGT014

**Referenced assembly was not compiled with the generator.** A warning, reported on the `AddAgentic()` call.

A referenced assembly declares toolsets or agents but carries no module stamp, so `AddAgentic()` cannot register
them. NuGet does not pass a package's analyzers on to the projects that reference the consumer: reference the Agentic
package in that assembly's project too.

## AGT015

**Tool name is not lower_snake_case.** Reported on the argument of `WithName(...)`.

Tool names follow the convention of the model providers' function names, `find_companies`. At runtime `WithName`
throws an `ArgumentException` for any other name.

## AGT016

**Settings type is less accessible than its toolset.** Reported on `WithSettings<T>`.

A public toolset exposes its settings through `Tools.{Tool}` (a `ToolRef<TSettings, TConfiguration>`) and through
the configuration views, whose properties have the types of the configurable members. Make the settings record and
those member types public, or make the toolset internal.

```csharp
internal record EchoSettings { public int Repeat { get; init; } }
public partial class EchoToolset : Toolset      // AGT016
internal partial class EchoToolset : Toolset    // fixed, or make EchoSettings public
```
