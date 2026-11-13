---
name: add-runtime-feature
description: Add or change public behavior of the Typeshift runtime library (src/Typeshift) - toolsets, tools, agents, hosts, stores and conversations. Use for any change a library user can observe, to find where code, tests and docs go.
---

# Add a runtime feature

1. **Decide.** Read the decisions the feature touches in `docs/contributing/decisions.md`. A new choice gets an entry
   (context, decision, alternatives, where); code that only makes sense with it cites it as `// Dnn: ...`.
2. **Code.** Public types live in the `Typeshift` namespace, what only generated code calls in `Typeshift.Generated`,
   hidden from editors (D10). Every public member has XML docs with each `param`, `typeparam` and `returns`
   (`PublicApiDocumentationTests`).
3. **Tests** in `tests/Typeshift.Tests`, one class per subject, following `AGENTS.md`, "Tests": the scripted
   `IChatClient` in `Testing/Fakes`, `HostTestBase` for anything that needs services, `AgentTestBase` for runs
   through `AddTypeshift()` and an `AgentHost<T>`, data from the catalog. A behavior across assemblies uses the
   toolsets of `tests/Typeshift.Tests.Toolsets`.
4. **Docs.** The guide page of the feature, its code from `samples/Typeshift.Samples.Docs` through mdsnippets; see the
   `update-docs` playbook.
5. **Status.** Add the behavior and its test to the table in `docs/contributing/status.md`; a known limit goes under
   "Known gaps", never into a TODO.
6. **Public API.** Any added, removed or changed public member fails `PublicApiSurfaceTests` and writes
   `tests/Typeshift.Generators.Tests/Snapshots/PublicApi.received.txt`. Read its difference to `PublicApi.verified.txt`:
   every line must be a change you meant. Then accept it with
   `ACCEPT_SNAPSHOTS=1 dotnet test --project tests/Typeshift.Generators.Tests -c Release --no-build`, commit the
   snapshot with the change, add the `CHANGELOG.md` line (a removed or changed member under Changed or Removed, with
   the call before and after), and build the demos. A member of `Typeshift.Generated`, of `IStorable<TSelf>` or
   `Agent.Declaration` is accepted only after you raised `ReferenceReader.StampVersion` (D7).
