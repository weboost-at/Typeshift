---
name: change-generated-code
description: Change what the Typeshift source generator reads or writes (tool functions, Overrides records, factories, tool lists, stamps, agents' Settings and declarations, host members, modules and the AddTypeshift interceptor). Use for any edit under src/Typeshift.Generators, and to debug what the generator emits.
---

# Change generated code

Read `docs/contributing/architecture.md`, "The generator", first.

## Rules

- The generator writes code only; the analyzer reports (D2). Both use the same parsers, the generator with
  `Reporter.None`. A shape the parser rejects is skipped by the generator, never half-emitted.
- Parsing and emitting stay apart. Models (`Models/`) hold only strings, bools, enums and `EquatableArray<T>`: no
  symbols, no syntax, no locations, or caching breaks.
- Generated code is `global::`-qualified, escapes keyword names (`Names.Identifier`), starts with the header of
  `SourceWriter`, documents every public member, and hides what only generated code calls with
  `EditorBrowsable(Never)` (D10).
- Generated code is not held to 120 columns. A signature stays on its first line, a long parameter list breaks after
  `(` with one parameter per line, and a `where` clause takes its own line after the `)`.
- A generated member a user member could share a name with is checked in the parser (TSH018), with
  `Names.ToolsetMembers` and `AgentParser.Members` as the lists of those names.
- An agent's picks from a tool list do not bind in the generator, whose own output the list is: read them from
  syntax, walking the chain with `ConstructorChains.GetChain`, never from symbols.
- What an agent needs of a toolset goes through `ToolsetSummary`, built from the model in the compilation and from the
  `[GeneratedTool]` stamps of a reference; a new field goes into both, and a stamp change raises
  `ReferenceReader.StampVersion` (D7).
- The generator targets Roslyn 5 (D8) and netstandard2.0: no `Index`, `Range` or list patterns.

## Steps

1. Change the parser and the model, then the emitter.
2. Run the generator tests; a snapshot mismatch writes `.received.txt` next to the `.verified.txt` in
   `tests/Typeshift.Generators.Tests/Snapshots`. Read the difference, then accept with `ACCEPT_SNAPSHOTS=1`.
3. Add a `CompilesCleanly` test for every new shape of user code, a keyword name or a nested class say.
4. Keep `IncrementalTests` green: an edit outside a toolset or agent keeps every output cached, and an agent's output
   stays cached while what it offers is unchanged.
5. Debug the output of a real project in its `obj/Release/net10.0/Typeshift.Generators/` folder.
