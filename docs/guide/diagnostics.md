# Diagnostics

An analyzer in the package reports these while you type; the generator only writes code. They sit at real source
locations, so `#pragma warning disable` and `.editorconfig` severities reach them. Every shape the generator cannot
handle has a diagnostic: generated code never fails with a compiler error of its own.

| ID | Severity | Title |
|---|---|---|
| [TSH001](#tsh001) | Error | Toolset or agent is not partial |
| [TSH002](#tsh002) | Error | Declared value is not a constant |
| [TSH003](#tsh003) | Error | Tool is not a static method group |
| [TSH004](#tsh004) | Error | Tool parameter cannot be bound |
| [TSH005](#tsh005) | Error | Settings type is not a record with a parameterless constructor |
| [TSH006](#tsh006) | Error | Settings are less accessible than what exposes them |
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
| [TSH024](#tsh024) | Warning | Sub-agent offers tools that need approval |
| [TSH025](#tsh025) | Error | Tool is offered twice |
| [TSH026](#tsh026) | Error | Agents call each other in a circle |
| [TSH028](#tsh028) | Error | Store has another id type |

## TSH001

**Toolset or agent is not partial**, or a class it is nested in is not. The generator adds members to it.

## TSH002

**Declared value is not a constant.** `WithName`, `WithDescription`, `WithStorageKind` and `WithSettingsVersion`
are read at compile time; pass a literal or a `const`.

## TSH003

**Tool is not a static method group.** `AddTool(Find)` with `Find` a public static method. Lambdas, instance methods,
local functions and delegate variables cannot be generated without reflection.

## TSH004

**Tool parameter cannot be bound**: `ref`, `out`, pointers and other parameters a model's JSON cannot fill.

## TSH005

**Settings type is not a record with a parameterless constructor.** Each layer applies to a `with` copy of the code
defaults.

## TSH006

**Settings are less accessible than what exposes them**: a settings record less accessible than its toolset, whose
generated members name it wherever the toolset can be named, or a toolset with configurable settings less accessible
than a public agent that offers it, whose `Settings` record names the toolset's overrides.

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

**Configurable member has a type settings cannot store**, or belongs to the run rather than to settings. Chat options
that may be configurable are the model, the instructions, the sampling options, the stop sequences, multiple tool calls
and reasoning; `Tools`, `ConversationId`, `ResponseFormat`, `ToolMode` and the additional properties are not.

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

**Referenced assembly was not compiled with the generator.** It declares toolsets or agents but carries no module of
this Typeshift version, so `AddTypeshift()` cannot register them. Reference the package in its project, at the version
the rest of the build uses.

## TSH021

**Referenced assembly has another id type.** Every project of a build shares one id type: set `TypeshiftIdType` once,
in `Directory.Build.props`.

## TSH022

**Two agents share a settings kind**: two agent classes of one name in different namespaces, or one
`WithStorageKind` twice. Their settings would overwrite each other in the store; give one a `WithStorageKind` of its
own.

## TSH023

**Agent cannot be a tool or a handoff target**: it has no `WithDescription`, which the model reads to decide when to
call it, or its name is not lower_snake_case. Checked for agents of the same project.

## TSH024

**Sub-agent offers tools that need approval.** A sub-agent cannot ask the host, so in the default mode its calls of
such tools are rejected unless a standing approval or a rule approves them. Add a rule, or hand off to the agent
instead.

## TSH025

**Tool is offered twice** by one agent: one toolset used twice, one tool picked twice, or one agent used twice as a
sub-agent or as a handoff target. Offer each toolset's tools in one use.

## TSH026

**Agents call each other in a circle** through `UseAgent`, so a call could never end. Handoffs may form circles.

## TSH028

**Store has another id type.** `WithStore<T>()` takes an `ITypeshiftStore` of the build's id type, the alias the
generator writes.
