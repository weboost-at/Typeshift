# Diagnostics

The generator reports these diagnostics while it reads toolsets, agents and the `AddAgentic()` call. Errors stop the
build before a mistake can reach runtime; warnings point at something that works but is probably not meant.

Two mistakes need no diagnostic, because the generated API makes them compiler errors:

- selecting a tool of another toolset, `UseToolset<CompanyToolset>().SearchWeb()`, is CS1929: the selection methods
  exist only on the selection of their own toolset;
- setting the settings of a tool the agent does not offer, `WithToolSettings().SearchWeb()` in an agent without
  `search_web`, does not compile either: the builder has a method only per tool the agent offers.

Runtime guards stay for what the generator cannot see: generic toolsets and agents, and calls made through a
delegate. Each guard throws an exception whose message says what to change.

| ID | Severity | Title |
|---|---|---|
| [AGT001](#agt001) | Error | Toolset or agent is not partial |
| [AGT002](#agt002) | Error | Tool name or description is not a constant |
| [AGT003](#agt003) | Error | Tool has no name |
| [AGT004](#agt004) | Error | Tool is not a static method group |
| [AGT005](#agt005) | Error | Settings type is not a record |
| [AGT006](#agt006) | Error | Toolset was not compiled with the generator |
| [AGT007](#agt007) | Error | Tool name is not unique |
| [AGT008](#agt008) | Warning | Tool has no description |
| [AGT009](#agt009) | Warning | Tool does not take its settings |
| [AGT010](#agt010) | Warning | Tool relies on XML docs that are not generated |
| [AGT011](#agt011) | Warning | Referenced assembly was not compiled with the generator |
| [AGT012](#agt012) | Error | Tool name is not lower_snake_case |
| [AGT013](#agt013) | Error | Settings type is less accessible than its toolset |
| [AGT014](#agt014) | Error | Configurable member has a type settings cannot store |

## AGT001

**Toolset or agent is not partial.** Reported on the class name.

The generator adds members to every toolset and agent (the tool bindings, the `Settings` record, the
`WithToolSettings` overloads), so the class must be `partial`.

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

Every tool needs `WithName(...)`. The name is the tool's identity in stored settings, in agents and across
assemblies, so it never comes from the method name, which a refactoring may change.

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

Stored and run overrides apply to a `with` copy of the settings, so the type must be a record. The code defaults
come from its parameterless constructor, which it therefore needs as well.

## AGT006

**Toolset was not compiled with the generator.** Reported on the `UseToolset<T>(...)` call.

The toolset comes from an assembly that carries no generator stamps, so its tools, their selection methods and their
settings are unknown. Reference the Agentic package in the toolset's project. At runtime, such a toolset throws an
`InvalidOperationException` when the agent asks it for a tool.

## AGT007

**Tool name is not unique.** Reported on the first own tool that uses the name, or on the `AddAgentic()` call when the
clash is only among referenced assemblies.

Tool names are global: stored settings, agents and the model address a tool by its name. Two tools with one name,
anywhere the host can see, are an error.

## AGT008

**Tool has no description.** A warning, reported on the `AddTool(...)` call.

The model chooses tools by their descriptions. Add `WithDescription(...)` or an XML `<summary>` on the method.

## AGT009

**Tool does not take its settings.** A warning, reported on `WithSettings<T>`.

A tool receives its resolved settings through a parameter of its settings type, which the model never sees. Without
one, the settings reach nothing. Add the parameter:

```csharp
public static Company[] Find(string city)                                  // AGT009
public static Company[] Find(string city, FindCompaniesSettings settings)  // fixed
```

## AGT010

**Tool relies on XML docs that are not generated.** A warning, reported on the `AddTool(...)` call.

A tool takes its description from its method's `<summary>` when it has no `WithDescription(...)`, and its parameter
descriptions from `<param>` docs. The compiler only reads them with
`<GenerateDocumentationFile>true</GenerateDocumentationFile>`, so without it they are lost. Turn it on; or, when only
the summary is at stake, describe the tool with `WithDescription(...)`.

## AGT011

**Referenced assembly was not compiled with the generator.** A warning, reported on the `AddAgentic()` call.

A referenced assembly declares toolsets or agents but carries no module stamp, so `AddAgentic()` cannot register
them. NuGet does not pass a package's analyzers on to the projects that reference the consumer: reference the Agentic
package in that assembly's project too.

## AGT012

**Tool name is not lower_snake_case.** Reported on the argument of `WithName(...)`.

Tool names follow the convention of the model providers' function names, `find_companies`.

## AGT013

**Settings type is less accessible than its toolset.** Reported on `WithSettings<T>`.

A public toolset exposes its settings through `Tools.{Tool}` (a `ToolRef<TSettings, TOverrides>`) and through the
generated `{Tool}Overrides` record, whose properties have the types of the configurable members. Make the settings
record and those member types public, or make the toolset internal.

```csharp
internal record EchoSettings { public int Repeat { get; init; } }
public partial class EchoToolset : Toolset      // AGT013
internal partial class EchoToolset : Toolset    // fixed, or make EchoSettings public
```

## AGT014

**Configurable member has a type settings cannot store.** Reported on the `Configurable(...)` call that picks it, for a
tool setting or a chat option.

Configurable members end up in the generated `Settings` and `{Tool}Overrides` records, which serialize themselves
without reflection. They support:

- `bool`, `byte`, `short`, `int`, `long`, `float`, `double` and `decimal`;
- `string`, `Guid`, `DateTime`, `DateTimeOffset` and `TimeSpan`;
- enums, stored by name;
- string lists: `string[]`, `IEnumerable<string>`, `IReadOnlyCollection<string>`, `IReadOnlyList<string>`,
  `ICollection<string>` and `IList<string>`;
- the nullable forms of all of these.

Keep other members out of `Configurable(...)`; their value then comes from code alone.
