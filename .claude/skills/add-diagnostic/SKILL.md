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
   `Warning(...)`: the ID the catalog in `docs/guide/diagnostics.md` reserves, or the next free one; a title in
   sentence case without a period; a message that names the offending thing and says what to do. Add it to
   `TypeshiftAnalyzer.SupportedDiagnostics`.
2. **Release row.** Add `TSHxxx | Typeshift | Error | <title>` to
   `src/Typeshift.Generators/AnalyzerReleases.Unshipped.md`. RS2000 fails the build without it. Released IDs never
   change or get reused (D49).
3. **Report it** where the information is (D2):
   - a rule about one declaration goes into its parser (`Parsing/ToolsetParser.cs`), through the `Reporter` it gets;
     the generator parses with `Reporter.None` and must skip, never emit, what does not parse;
   - a rule about one call goes into `TypeshiftAnalyzer.AnalyzeCall`;
   - a rule across declarations goes into a compilation-end action of `TypeshiftAnalyzer`.
   - Report on the narrowest syntax the developer has to change (the argument, the `WithSettings<T>` name).
4. **Test.** Add a source constant to `tests/Typeshift.Generators.Tests/Diagnostics/Support/DiagnosticSources.cs` and a
   test to `DiagnosticTests.cs`, named `Given_<state>_TSHxxx_Is_Reported_On_<where>`:

   ```csharp
   var run = Run(DiagnosticSources.YourCase);
   run.Assert.Reports("TSHxxx", DiagnosticSeverity.Error, "<the exact offending syntax text>");
   ```

   The theory over every source in `DiagnosticSources` checks that the generated code holds no compiler error.
5. **Docs.** In `docs/guide/diagnostics.md`, add the table row `| [TSHxxx](#tshxxx) | Error | <title> |` and a
   `## TSHxxx` section: the bold title, why it is a mistake, how to fix it. Link it from the guide page of the
   feature it guards.
6. **Changelog.** One line under `[Unreleased]` in `CHANGELOG.md`.

`DiagnosticCatalogTests` fails when a released ID has no docs row, no docs section or no `..._TSHxxx_Is_Reported_...`
test.

## Check

```shell
dotnet build -c Release -warnaserror
dotnet test --solution Typeshift.slnx -c Release --no-build
```
