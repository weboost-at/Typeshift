---
name: add-runtime-feature
description: Add or change public behavior of the Typeshift runtime library (src/Typeshift) - toolsets, tools, agents, hosts, stores and conversations. Use for any change a library user can observe, to find where code, tests and docs go.
---

# Add a runtime feature

1. **Decide.** Read the decisions the feature touches in `docs/contributing/decisions.md`. A new choice gets an entry
   (context, decision, alternatives, where); code that only makes sense with it cites it as `// Dnn: ...`.
2. **Code.** Public types live in the `Typeshift` namespace, what only generated code calls in `Typeshift.Generated`,
   hidden from editors (D10). Every public member has XML docs with each `param`, `typeparam` and `returns`, and an
   `<exception>` for each exception its code throws (`PublicApiDocumentationTests`). A new exception type is named
   in the docs of what throws it. When a run, an answer, a resume or a settings member of the host throws it, that is
   a generated member: add the tag where `AgentEmitter` writes the member's docs, and the type to its row of
   `AgentGenerationTests.Given_A_Host_Member_That_Throws_Its_Docs_Name_The_Exception`. A new log event is a method
   of `Telemetry/LogEvents.cs` with the next id and a row in `docs/guide/observability.md` (`LogEventCatalogTests`).
3. **Native AOT.** The build's trim and AOT analyzers see each project alone. The two publishes of the console sample
   in `AGENTS.md`, "Build and test", compile the whole app, as Native AOT and trimmed, and run it: they are the only
   place an IL2xxx or IL3xxx warning of the whole program shows, or a type whose JSON metadata is missing fails.
   Publish and run both after a change that serializes, reflects or creates types.
4. **Tests** in `tests/Typeshift.Tests`, one class per subject, following `AGENTS.md`, "Tests": the scripted
   `IChatClient` in `Testing/Fakes`, `HostTestBase` for anything that needs services, `AgentTestBase` for runs
   through `AddTypeshift()` and an `AgentHost<T>`, data from the catalog. A behavior across assemblies uses the
   toolsets of `tests/Typeshift.Tests.Toolsets`.
5. **Status.** Add the behavior and its test to the table in `docs/contributing/status.md`; a known limit goes under
   "Known gaps", never into a TODO.
6. **Public API.** Any added, removed or changed public member fails `PublicApiSurfaceTests` and writes
   `tests/Typeshift.Generators.Tests/Snapshots/PublicApi.received.txt`. Read its difference to `PublicApi.verified.txt`:
   every line must be a change you meant. Then accept it with
   `ACCEPT_SNAPSHOTS=1 dotnet test --project tests/Typeshift.Generators.Tests -c Release --no-build` and commit the
   snapshot with the change. A member of `Typeshift.Generated`, of `IStorable<TSelf>` or `Agent.Declaration` is
   accepted only after you raised `KnownTypes.StampVersion` (D7). A removed or changed member is a break: follow its
   row, "Removed or changed public API", in the change checklist of `AGENTS.md`.

## Finish

- **Changelog.** A line in `CHANGELOG.md` for what a user can observe, by the rule in `AGENTS.md`, "Commits".
- **Docs.** The pages the change checklist under "Docs" in `AGENTS.md` names for your change, the code they show
  from `samples/Typeshift.Samples.Docs`, through the `update-docs` playbook.
- **Demos.** After a change of public API, of generated code or of a diagnostic, build the demos as `AGENTS.md`,
  "Build and test", shows; fix a demo you broke, or say so under the **Demos** heading of the pull request.
- **Check.** The commands of `AGENTS.md`, "Build and test", with the suite green three times in a row.
