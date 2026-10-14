---
name: add-runtime-feature
description: Add or change public behavior of the Typeshift runtime library (src/Typeshift) - toolsets, tools, and the agents, hosts, stores and conversations the later slices add. Use for any change a library user can observe, to find where code, tests and docs go.
---

# Add a runtime feature

1. **Decide.** Read the decisions the feature touches in `docs/contributing/decisions.md`. A new choice gets an entry
   (context, decision, alternatives, where); code that only makes sense with it cites it as `// Dnn: ...`.
2. **Code.** Public types live in the `Typeshift` namespace, what only generated code calls in `Typeshift.Generated`,
   hidden from editors (D10). Every public member has XML docs with each `param`, `typeparam` and `returns`
   (`PublicApiDocumentationTests`).
3. **Tests** in `tests/Typeshift.Tests`, one class per subject, following `AGENTS.md`, "Tests": the scripted
   `IChatClient` in `Testing/Fakes`, `HostTestBase` for anything that needs services, data from the catalog.
4. **Docs.** The guide page of the feature, its code from `samples/Typeshift.Samples.Docs` through mdsnippets; see the
   `update-docs` playbook.
5. **Status.** Add the behavior and its test to the table in `docs/contributing/status.md`; a known limit goes under
   "Known gaps", never into a TODO.
