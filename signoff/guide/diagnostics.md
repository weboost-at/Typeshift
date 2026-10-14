# Diagnostics

An analyzer in the package reports these while you type; the generator only writes code. They sit at real source
locations, so `#pragma warning disable` and `.editorconfig` severities reach them. Every shape the generator cannot
handle has a diagnostic: generated code never fails with a compiler error of its own.

Two mistakes need no diagnostic, since the generated API makes them compiler errors: picking a tool of another
toolset (`t.SearchWeb` is not on that toolset's list) and configuring a tool the agent does not offer (it has no
`Configure{Tool}()`).

| ID | Severity | Title |
|---|---|---|
| [TSH001](#tsh001) | Error | Toolset or agent is not partial |
| [TSH002](#tsh002) | Error | Name or description is not a constant |
| [TSH003](#tsh003) | Error | Tool is not a static method group |
| [TSH004](#tsh004) | Error | Tool parameter cannot be bound |
| [TSH005](#tsh005) | Error | Settings type is not a record with a parameterless constructor |
| [TSH006](#tsh006) | Error | Settings type is less accessible than its toolset |
| [TSH007](#tsh007) | Error | Tool name is not unique |
| [TSH008](#tsh008) | Error | Tool name is not lower_snake_case |
| [TSH009](#tsh009) | Warning | Tool has no description |
| [TSH010](#tsh010) | Warning | Tool does not take its settings |
| [TSH011](#tsh011) | Warning | Tool relies on XML docs that are not generated |
| [TSH012](#tsh012) | Error | Configurable member has a type settings cannot store |
| [TSH013](#tsh013) | Error | Configurable member cannot be set |
| [TSH014](#tsh014) | Error | Declaration outside the constructor |
| [TSH015](#tsh015) | Error | Chain the generator cannot read |
| [TSH016](#tsh016) | Error | Toolset or agent is out of the generated code's reach |
| [TSH017](#tsh017) | Error | Toolset or agent is generic or derives from another |
| [TSH018](#tsh018) | Error | Member clashes with a generated member |
| [TSH019](#tsh019) | Error | Toolset was not compiled with the generator |
| [TSH020](#tsh020) | Warning | Referenced assembly was not compiled with the generator |
| [TSH021](#tsh021) | Error | Referenced assembly has another id type |
| [TSH022](#tsh022) | Error | Two agents share a settings kind |
| [TSH023](#tsh023) | Error | Agent cannot be a tool or a handoff target |
| [TSH024](#tsh024) | Error | Agent used as a tool needs approval |
| [TSH025](#tsh025) | Error | Tool is offered twice |
| [TSH026](#tsh026) | Error | Agents call each other in a circle |
| [TSH027](#tsh027) | Error | Tool type has no JSON metadata under Native AOT |
| [TSH028](#tsh028) | Error | Store has another id type |

## TSH001

**Toolset or agent is not partial**, or a class it is nested in is not. The generator adds members to it.

## TSH002

**Name or description is not a constant.** `WithName` and `WithDescription` are read at compile time; pass a literal
or a `const`.

## TSH003

**Tool is not a static method group.** `AddTool(Find)` with `Find` a public static method. Lambdas, instance methods,
local functions and delegate variables cannot be generated without reflection.

## TSH004

**Tool parameter cannot be bound**: `ref`, `out`, pointers and other parameters a model's JSON cannot fill.

## TSH005

**Settings type is not a record with a parameterless constructor.** Each layer applies to a `with` copy of the code
defaults.

## TSH006

**Settings type is less accessible than its toolset.** The generated tool list and `Configure{Tool}()` name it
wherever the toolset can be named.

## TSH007

**Tool name is not unique** across everything the host sees, including names that differ only in their underscores
(`get_v2`, `get_v_2`), which map to the same generated property.

## TSH008

**Tool name is not lower_snake_case**, the convention of the providers' function names. Reported on `WithName`.

## TSH009

**Tool has no description.** The model chooses tools by their descriptions: add a `<summary>` or `WithDescription`.

## TSH010

**Tool does not take its settings.** A tool with `WithSettings<T>()` but no parameter of type `T` gets settings that
reach nothing.

## TSH011

**Tool relies on XML docs that are not generated.** Set `GenerateDocumentationFile`, or describe the tool with
`WithDescription`.

## TSH012

**Configurable member has a type settings cannot store**; see [the storable types](./tool-settings.md#configurable-members).
Under `WithConfigurableSettings<T>()`, keep it out with `NotConfigurable`.

## TSH013

**Configurable member cannot be set**: it has no public `set` or `init` accessor.

## TSH014

**Declaration outside the constructor.** `AddTool`, `UseToolset` and the other declarations go directly in the
constructor, not in a helper method it calls.

## TSH015

**Chain the generator cannot read**: a chain split over variables (`var u = UseToolset<T>(); u.Include(...)`), or a
pick that is not `t => t.Tool`. Keep each declaration one expression.

## TSH016

**Toolset or agent is out of the generated code's reach**: `private` or `protected` nested, or without an accessible
parameterless constructor.

## TSH017

**Toolset or agent is generic or derives from another.** Each derives from `Toolset` or `Agent` directly; share code
through static methods.

## TSH018

**Member clashes with a generated member**, a toolset member named `Tools` or an agent member named `Settings` or
`ConfigureSearchTickets`, say.

## TSH019

**Toolset was not compiled with the generator.** Reported on `UseToolset<T>()`: reference the package in the
toolset's project.

## TSH020

**Referenced assembly was not compiled with the generator.** It declares toolsets or agents but carries no module
stamp, so `AddTypeshift()` cannot register them. Reference the package in its project.

## TSH021

**Referenced assembly has another id type.** Set `TypeshiftIdType` once, in `Directory.Build.props`.

## TSH022

**Two agents share a settings kind**: two agent classes of one name in different namespaces. Rename one.

## TSH023

**Agent cannot be a tool or a handoff target**: it has no description, or its name is not lower_snake_case.

## TSH024

**Agent used as a tool needs approval**: it offers a tool that requires approval, and a run called as a tool has
nobody to ask. Hand off to it instead.

## TSH025

**Tool is offered twice** by one agent, through two uses of one toolset or a toolset and an MCP server's static name.

## TSH026

**Agents call each other in a circle** through `UseAgent`. Handoffs may form circles.

## TSH027

**Tool type has no JSON metadata under Native AOT.** In a project that publishes with Native AOT, list the type in a
`JsonSerializerContext` of the toolset's assembly; see [Native AOT](./native-aot.md).

## TSH028

**Store has another id type.** `WithStore<T>()` takes an `ITypeshiftStore` of the build's id type.
