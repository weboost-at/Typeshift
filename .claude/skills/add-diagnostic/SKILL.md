---
name: add-diagnostic
description: Add or change a generator diagnostic (AGTxxx) in Agentic. Use when the generator should report a new mistake in toolsets, agents or the AddAgentic() call, or when a diagnostic's message, severity or location changes.
---

# Add a diagnostic

First ask whether the generated API can make the mistake a compiler error instead: selecting a foreign tool and setting
the settings of a tool the agent does not offer need no diagnostic, because no generated member accepts them (D29,
D30 in `docs/contributing/decisions.md`). A diagnostic is for what the types cannot express.

## Steps

1. **Descriptor.** In `src/Agentic.Generators/Diagnostics/Descriptors.cs`, add a field with `Error(...)` or
   `Warning(...)`: the next free ID, a title in sentence case without a period, a message that names the offending
   thing and says what to do. Add it to the `All` array.
2. **Release row.** Add `AGTxxx | Agentic | Error | <title>` to `src/Agentic.Generators/AnalyzerReleases.Unshipped.md`.
   The RS2000 analyzer fails the build without it. Shipped IDs never change or get reused (D36).
3. **Report it** where the information is:
   - toolset rules in `Parsing/ToolsetParser.cs`, agent rules in `Parsing/AgentParser.cs`, rules that need every
     toolset in `AgentResolver.cs`, registration rules in `Diagnostics/RegistrationDiagnostics.cs`;
   - through the model's `DiagnosticBag` (`bag.Report(Descriptors.X, node, args)`), never as a `Diagnostic` or
     `Location` in a model: models must stay equatable or every edit regenerates everything.
   - Report on the narrowest syntax the developer has to change (the argument, the `WithSettings<T>` name), not the
     whole class.
4. **Runtime guard.** If the same mistake can reach runtime past the generator (generic classes, calls through a
   delegate), the runtime throws with a message saying what to change. Test it in `tests/Agentic.Tests`.
5. **Test.** Add a source constant to `tests/Agentic.Generators.Tests/Diagnostics/Support/DiagnosticSources.cs` and a
   test to `DiagnosticTests.cs`, named `Given_<state>_AGTxxx_Is_Reported_On_<where>`:

   ```csharp
   var run = Run(DiagnosticSources.YourCase);
   run.Assert.Reports("AGTxxx", DiagnosticSeverity.Error, "<the exact offending syntax text>");
   ```

   For a warning that must stay quiet in a nearby case, add a `..._Is_Not_Reported` test with `DoesNotReport`.
6. **Docs.** In `docs/guide/diagnostics.md`, add the table row `| [AGTxxx](#agtxxx) | Error | <title> |` and a
   `## AGTxxx` section: the bold title and where it is reported, why it is a mistake, how to fix it, an example when
   the fix is not obvious. Link it from the guide page of the feature it guards.
7. **Changelog.** One line under `[Unreleased]` in `CHANGELOG.md`.

`DiagnosticCatalogTests` fails when a released ID has no docs row, no docs section or no test named after it.

## Check

```shell
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
npm run docs:build
```
