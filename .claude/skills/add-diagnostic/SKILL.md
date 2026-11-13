---
name: add-diagnostic
description: Add or change a Typeshift diagnostic (TSHxxx). Use when the analyzer should report a new mistake in toolsets, agents or the AddTypeshift() call, or when a diagnostic's message, severity or location changes.
---

# Add a diagnostic

First ask whether the generated API can make the mistake a compiler error instead: picking a foreign tool and
configuring a tool the agent does not offer need no diagnostic, because no generated member accepts them (D4, D20 in
`docs/contributing/decisions.md`). A diagnostic is for what the types cannot express.

## Steps

1. **Descriptor.** In `src/Typeshift.Generators/Diagnostics/Descriptors.cs`, add a field with `Error(...)` or
   `Warning(...)`. Its ID is the number after the highest ever used, counting the "Retired IDs" of
   `docs/guide/diagnostics.md`. Never fill a gap: a number a tag once shipped may still sit in someone's `#pragma`
   or `<NoWarn>` (D49). Give it a title in sentence case without a period and a message that names the offending
   thing and says what to do, and add it to `TypeshiftAnalyzer.SupportedDiagnostics`.
2. **Release row.** Add `TSHxxx | Typeshift | Error | <title>` to
   `src/Typeshift.Generators/AnalyzerReleases.Unshipped.md`. RS2000 fails the build without it.
3. **Report it** where the information is (D2):
   - a rule about one declaration goes into its parser, `Parsing/ToolsetParser.cs` for a tool and its settings or
     `Parsing/AgentParser.cs` for what an agent declares, through the `Reporter` it gets; the generator parses with
     `Reporter.None` and must skip, never emit, what does not parse;
   - a rule about one call goes into `TypeshiftAnalyzer.AnalyzeCall`;
   - a rule across declarations goes into a compilation-end action of `TypeshiftAnalyzer`.
   - Report on the narrowest syntax the developer has to change (the argument, the `WithSettings<T>` name).
   - Say nothing about a type that does not resolve yet (`ContainsErrorType()`): the compiler reports it, its name
     may be empty, and the generator skips what names it.
4. **Test.** Add a source constant to `tests/Typeshift.Generators.Tests/Diagnostics/Support/DiagnosticSources.cs` and a
   test to `DiagnosticTests.cs`, named `Given_<state>_TSHxxx_Is_Reported_On_<where>`:

   ```csharp
   var run = Run(DiagnosticSources.YourCase);

   run.Assert.Reports(
       "TSHxxx",
       DiagnosticSeverity.Error,
       "<the exact offending syntax text>",
       message: "<the whole message, as the developer reads it>");
   ```

   The first test of an ID states the whole message, so swapped arguments fail it; later tests of the ID leave
   `message` out. `Reports`, `ReportsAmong`, `ReportsOnTheCompilation` and `DoesNotReport` also fail when the run
   reports anything beside the ID: a second diagnostic of the analyzer, or a compiler error in the source constant.
   A mistake that two checks see names the other one, `also: "TSH020"`, and so does code still being typed,
   `also: "CS7036"`.

   The theory over every source in `DiagnosticSources` checks that the generated code holds no compiler error, and
   that neither the generator nor the analyzer throws. A shape the parser accepts after your change is a field of
   `ShapeSources` instead, and a statement that made one of them throw is an entry of `HalfTypedSources`; both sit
   beside `DiagnosticSources` and have a theory of their own. A mistake that spans a referenced project takes its
   sources from `Support/ReferenceSources.cs` and compiles the reference with `CompileReference`.
5. **Docs.** In `docs/guide/diagnostics.md`, add the table row `| [TSHxxx](#tshxxx) | Error | <title> |` and a
   `## TSHxxx` section: the bold title, why it is a mistake, how to fix it. Link it from the guide page of the
   feature it guards.
6. **Changelog.** One line under `[Unreleased]` in `CHANGELOG.md`.

`DiagnosticCatalogTests` fails when an ID has no docs row, no docs section or no `..._TSHxxx_Is_Reported_...` test,
when a number below the highest is neither a diagnostic nor a retired row, and when a retired number is a diagnostic
again.

## Change or remove one

- **A title or a severity changes.** The descriptor, the row in `AnalyzerReleases.Unshipped.md`, the row in the
  table of `docs/guide/diagnostics.md` and the bold title of its section change together, with a line in
  `CHANGELOG.md`. Before 1.0 an ID's meaning may change this way; its number stays.
- **A diagnostic goes away.** Remove the descriptor, its release row, its docs row and section and its tests, and
  add the ID to the "Retired IDs" table of `docs/guide/diagnostics.md` with its last title and the version that
  dropped it. The number is never used again.

## Check

Build and run the whole suite with the commands of `AGENTS.md`, "Build and test".
