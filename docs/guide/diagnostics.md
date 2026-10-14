# Diagnostics

An analyzer in the package reports these while you type; the generator only writes code. They sit at real source
locations, so `#pragma warning disable` and `.editorconfig` severities reach them. Every shape the generator cannot
handle has a diagnostic: generated code never fails with a compiler error of its own.

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
| [TSH013](#tsh013) | Error | Configurable member cannot be set |
| [TSH014](#tsh014) | Error | Declaration outside the constructor |
| [TSH015](#tsh015) | Error | Chain the generator cannot read |
| [TSH016](#tsh016) | Error | Toolset or agent is out of the generated code's reach |
| [TSH017](#tsh017) | Error | Toolset or agent is generic or derives from another |
| [TSH018](#tsh018) | Error | Member clashes with a generated member |

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
