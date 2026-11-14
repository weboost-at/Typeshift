---
name: change-generated-code
description: Change what the Typeshift source generator reads or writes (tool functions, Overrides records, factories, tool lists, stamps, agents' Settings and declarations, host members, modules and the AddTypeshift interceptor). Use for any edit under src/Typeshift.Generators, and to debug what the generator emits.
---

# Change generated code

Read `docs/contributing/architecture.md`, "The generator and the analyzer", first.

## Rules

- The generator writes code only; the analyzer reports (D2). Both use the same parsers, the generator with
  `Reporter.None`. A shape the parser rejects is skipped by the generator, never half-emitted.
- Neither may throw, whatever the code looks like while it is typed: an exception in the generator drops every
  generated file of the project (CS8785), one in the analyzer is AD0001. A symbol of half-typed code can be an error
  type, which has no containing assembly and an empty name.
- Parsing and emitting stay apart. Models (`Models/`) hold only strings, bools, enums and `EquatableArray<T>`: no
  symbols, no syntax, no locations, or caching breaks.
- Generated code is `global::`-qualified, escapes keyword names (`Names.Identifier`), starts with the header of
  `SourceWriter`, documents every public member, and hides what only generated code calls with
  `EditorBrowsable(Never)` (D10).
- Every generated type, and every member generated into a class of the user's, is written after `Names.Generated`,
  which marks it `[GeneratedCode]` and `[ExcludeFromCodeCoverage]`. Never mark the user's partial class: analyzers
  skip what the attribute marks, the user's own code in the class included.
- A generated class that other files must see without importing anything, such as a host or a tool list, lives in
  the global namespace under `Names.GlobalClassName` (D12). A generated record reads its own members through
  `this.`, since a settings member may be named like a parameter or a local of the generated method.
- Generated code is not held to 120 columns. A signature stays on its first line, a long parameter list breaks after
  `(` with one parameter per line, and a `where` clause takes its own line after the `)`.
- A generated member a user member could share a name with is checked in the parser (TSH018), with
  `Names.ToolsetMembers` and `AgentParser.Members` as the lists of those names.
- An agent's picks from a tool list do not bind in the generator, whose own output the list is: read them from
  syntax, walking the chain with `ConstructorChains.GetChain`, never from symbols.
- The generator parses a compilation without any source generator's output, its own included, and the analyzer one
  with all of it. A declaration that names such code is dropped by the generator alone, so the analyzer compares
  its model with the stamps of the generated code (TSH032, `TypeshiftAnalyzer.ReportAgentsReadDifferently` and
  `ParseToolset`). What a declaration can lose that way and a stamp holds belongs in that comparison.
- What an agent needs of a toolset goes through `ToolsetSummary`, built from the model in the compilation and from the
  `[GeneratedTool]` stamps of a reference; a new field goes into both, and a stamp change raises
  `ReferenceReader.StampVersion` (D7).
- `StampVersion` is the version of everything generated code binds to. Raise it too when a member of
  `Typeshift.Generated`, of `IStorable<TSelf>` or `Agent.Declaration` is added, removed or changed:
  `PublicApiSurfaceTests` fails until you do, even with `ACCEPT_SNAPSHOTS=1`. Then accept the snapshots, as in step 2:
  the modules in the generator snapshots carry the version, and `GeneratedContract.verified.txt` lists the members.
- The generator targets Roslyn 5 (D8) and netstandard2.0: no `Index`, `Range` or list patterns.

## Steps

1. Change the parser and the model, then the emitter.
2. Run the generator tests; a snapshot mismatch writes `.received.txt` next to the `.verified.txt` in
   `tests/Typeshift.Generators.Tests/Snapshots`. Read the difference, then accept with
   `ACCEPT_SNAPSHOTS=1 dotnet test --project tests/Typeshift.Generators.Tests -c Release --no-build`.
3. Put every shape of user code the change touches into a catalog under
   `tests/Typeshift.Generators.Tests/Diagnostics/Support`, one field or entry each. A theory runs every one of a
   catalog, so a shape is fixed once and stays fixed:
   - a shape the parser now accepts, a keyword name or a nested class for example, is a field of `ShapeSources`: the
     code generated for it must hold no error or warning;
   - a shape the parser rejects is a field of `DiagnosticSources`, with its diagnostic's test (the `add-diagnostic`
     playbook);
   - a statement that made the generator or the analyzer throw is an entry of `HalfTypedSources`, as it stands while
     it is typed.

   Add a `CompilesCleanly` test of its own where the user's code must compile too, a call to a generated member for
   example.
4. Keep `IncrementalTests` green: an edit outside a toolset or agent keeps every output cached, and an agent's output
   stays cached while what it offers is unchanged.
5. Debug the output of a real project: no project keeps the generated files, so build it with
   `dotnet build <project> -c Release -p:EmitCompilerGeneratedFiles=true` and read them in its
   `obj/Release/net11.0/generated/Typeshift.Generators/Typeshift.Generators.TypeshiftGenerator/` folder.
