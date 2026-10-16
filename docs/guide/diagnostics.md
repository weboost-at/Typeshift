---
title: Diagnostics
description: Every TSHnnn error and warning the Typeshift analyzer reports, what causes it and how to fix it.
---

# Diagnostics

The analyzer reports these errors and warnings while you type. Every mistake the generator cannot handle has one, so you
never debug a compiler error inside generated code.

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
| [TSH027](#tsh027) | Error | Tool type has no JSON metadata under Native AOT |
| [TSH028](#tsh028) | Error | Store has another id type |
| [TSH029](#tsh029) | Error | Id type cannot be found |

## Severities and suppression

An error fails the build. A warning lets it pass, but points at code that does not do what it seems to, such as
settings that reach no parameter ([TSH010](#tsh010)). Each diagnostic's help link leads to its section on this page.

Most diagnostics sit on the declaration they are about, so the usual
[suppression](https://learn.microsoft.com/dotnet/fundamentals/code-analysis/suppress-warnings) reaches them:
`#pragma warning disable TSH009` around one declaration, or a `dotnet_diagnostic.TSH009.severity` entry in
`.editorconfig`. [TSH020](#tsh020), [TSH021](#tsh021) and [TSH029](#tsh029) concern the whole build and have no
source location, so no `#pragma` reaches them.

## TSH001

**Toolset or agent is not partial.** The generator adds members to every toolset and agent, so the class and every
class it is nested in must be `partial`.

## TSH002

**Declared value is not a constant.** The generator reads `WithName`, `WithDescription`, `WithStorageKind` and
`WithSettingsVersion` at compile time. Pass a literal or a `const`.

## TSH003

**Tool is not a static method group.** Lambdas, instance methods, local functions and delegate variables need
reflection to call. Pass a static method, of any accessibility: `AddTool(Find)`.

## TSH004

**Tool parameter cannot be bound.** The model's JSON arguments cannot fill a `ref` or `out` parameter, a pointer, or
a ref struct such as `Span<T>`.

## TSH005

**Settings type is not a record with a parameterless constructor.** That constructor sets the code defaults, and each
layer of settings applies to a `with` copy of them.

## TSH006

**Settings are less accessible than what exposes them.** Make the record or the toolset as accessible as what exposes
it. There are two cases:

- A settings record is less accessible than its toolset. The toolset's generated members name the record wherever
  the toolset can be named.
- A toolset with configurable settings is less accessible than a public agent that offers it. The agent's `Settings`
  record names the toolset's overrides.

## TSH007

**Tool name is not unique** across everything the host sees. Names that differ only in their underscores, such as
`get_v2` and `get_v_2`, count as the same, because they map to the same generated property.

## TSH008

**Tool name is not lower_snake_case**, the convention of the providers' function names. It is reported on
`WithName`, or on the method when the name derived from it breaks the convention.

## TSH009

**Tool has no description.** The model chooses tools by their descriptions. Add a `<summary>` to the method, or call
`WithDescription`.

## TSH010

**Tool does not take its settings.** The tool has `WithSettings<T>()` but no parameter of type `T`, so its settings
reach nothing. Add the parameter to the method.

## TSH011

**Tool relies on XML docs that are not generated.** The compiler reads XML docs only with
`GenerateDocumentationFile`, so set it in the project. `WithDescription` is a way around it only when the parameters
have no `<param>` docs, since it replaces only the `<summary>`.

## TSH012

**Configurable member has a type settings cannot store**, or it belongs to the run rather than to settings.

- [Tool settings](./tool-settings.md#configurable-members) lists the types a configurable settings member may have.
- Of the chat options, the model, the instructions, the sampling options, the stop sequences, multiple tool calls and
  reasoning may be configurable. `Tools`, `ConversationId`, `ResponseFormat`, `ToolMode` and the additional
  properties may not, as [Builders](./builders.md#chatoptionsbuilder) shows.

## TSH013

**Configurable member cannot be set.** Give it a public `set` or `init` accessor.

## TSH014

**Declaration outside the constructor.** The generator reads `AddTool`, `UseToolset` and the other declarations only
directly in the constructor, not in a helper method, a lambda or a local function.

## TSH015

**Chain the generator cannot read.** Keep each declaration one expression. The generator cannot follow a chain split
over variables, such as `var u = UseToolset<T>(); u.Include(...)`, or a pick that is not `t => t.Tool`.

## TSH016

**Toolset or agent is out of the generated code's reach.** Generated code names the class and the module creates it,
so it cannot be nested as `private` or `protected`, and needs a public or internal parameterless constructor.

## TSH017

**Toolset or agent is generic or derives from another.** Derive each toolset from `Toolset` and each agent from
`Agent` directly, without type parameters. Share code through static methods.

## TSH018

**Member clashes with a generated member.** A member of yours has a name the generator writes, such as `Settings` in
an agent, or `CreateSearchTickets`, a tool's factory, in a toolset. Rename your member.
[Generated members](./generated-members.md) lists the names.

## TSH019

**Toolset was not compiled with the generator.** It is reported on `UseToolset<T>()`. Reference the package in the
toolset's project.

## TSH020

**Referenced assembly was not compiled with the generator.** It declares toolsets or agents but carries no module of
this Typeshift version, so `AddTypeshift()` cannot register them. Reference the package in its project, at the
version the rest of the build uses.

## TSH021

**Referenced assembly has another id type.** Every project of a build shares one id type. Set `TypeshiftIdType`
once, in `Directory.Build.props`.

## TSH022

**Two agents share a settings kind**, so their settings would overwrite each other in the store. It happens when two
agent classes have one name in different namespaces, or two agents set the same `WithStorageKind`. Give one of them
a `WithStorageKind` of its own.

## TSH023

**Agent cannot be a tool or a handoff target.** The model needs the agent's `WithDescription` to decide when to call
it, and a lower_snake_case name to call it by. The analyzer checks agents of the same project.

## TSH024

**Sub-agent offers tools that need approval.** A sub-agent cannot ask the host, so in the default mode its calls of
such tools are rejected unless a standing approval or a rule approves them. Add an
[approval rule](./tool-approval.md#approval-rules), or hand off to the agent instead.

## TSH025

**Tool is offered twice** by one agent, which happens when it:

- uses one toolset twice;
- picks one tool twice;
- uses one agent twice as a sub-agent, or twice as a handoff target;
- uses two toolsets with a tool of one name, a built-in toolset's included. Leave one of the two tools out.

## TSH026

**Agents call each other in a circle** through `UseAgent`, so a call could never end. Break the circle, or turn one
link into a handoff: handoffs may form circles.

## TSH027

**Tool type has no JSON metadata under Native AOT.** In a project that publishes with Native AOT or sets
`IsAotCompatible`, every parameter and result type of a tool needs source-generated metadata. List the type in a
`JsonSerializerContext` of the toolset's assembly ([Native AOT](./production.md#native-aot)). Strings, numbers,
`bool`, `char`, dates, `TimeSpan`, `IEnumerable<string>` and the JSON element and node types need none.

## TSH028

**Store has another id type.** `WithStore<T>()` takes a store of the build's id type. Implement `ITypeshiftStore`,
the alias the generator writes for it.

## TSH029

**Id type cannot be found.** `TypeshiftIdType` names a type the build does not know. Give a C# keyword (`int`,
`long`, `string`), a type of `System` (`Guid`), or the type's full name with its namespace.

## Related

- [Toolsets](./toolsets.md): the rules most toolset diagnostics guard.
- [Agents](./defining-agents.md): the rules of an agent's declarations.
- [Builders](./builders.md): every call the analyzer checks.
- [Registration](./registration.md): the package, the id type and what each project needs.
- [Native AOT](./production.md#native-aot): the JSON metadata [TSH027](#tsh027) asks for.
