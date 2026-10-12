---
name: add-runtime-feature
description: Add or change public behavior of the Agentic runtime library (src/Agentic) - agents, the runtime, stores, conversations, JSON. Use for any change a library user can observe, to find where code, tests and docs go.
---

# Add a runtime feature

## Before writing code

- Find the subject in `docs/contributing/architecture.md` and its decisions (`Dnn` in `docs/contributing/decisions.md`).
  Code comments starting `// Dnn:` point at the decision that explains odd-looking code; don't undo one without
  reading it. If you change a decision, edit its entry in the same commit, or add a new one and mark the old
  "Withdrawn"; `DecisionCitationTests` fails on citations of withdrawn or missing decisions.
- Check `docs/contributing/status.md` for known gaps and next steps on the subject.

## Where things go

| Concern | Place |
|---|---|
| Code | `src/Agentic/<Feature>/`, namespace = folder. Records for values and stored data, classes for behavior. |
| Typed per-agent API | Generated (`AgentEmitter` runtime extension block) calling a hidden `...Core` method on `AgentRuntime` (D4, D35). |
| Persistence | Through `IAgenticStore<TId>` only, data as `IStorable<TSelf>`; without a store, throw via `Stores.Get` (D26). |
| Time | Inject `TimeProvider`; never read the clock. |
| Tests | `tests/Agentic.Tests/<Area>/`, one class per subject, bases and data in `Testing/` or `<Area>/Support/`. |
| Docs | The guide page of the feature, its code in `samples/Agentic.Samples.Docs` (skill `update-docs`). |

## Public API rules

- Full XML docs on every public member: `<summary>`, every `<param>` and `<typeparam>`, `<returns>` for a result,
  `<exception>` for what a caller can act on. `PublicApiDocumentationTests` fails on a missing tag.
- No reflection over user types, no static mutable state, JSON through `JsonTypeInfo` (Native AOT).
- A breaking change goes to `CHANGELOG.md` under `[Unreleased]`.

## Tests

Read the test rules in `AGENTS.md`; the convention tests enforce them. Runtime behavior is tested through a real
`FunctionInvokingChatClient` loop with the scripted provider of `AgentTestBase`, against toolsets of the second
assembly (`tests/Agentic.Tests.Toolsets`) where registration matters. Data comes from `Data` (the catalog), time
moves only through `FakeTimeProvider.Advance`.

## Done means

```shell
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build            # three times, green every time
dotnet run --project samples/Agentic.Samples.Docs -c Release --no-build
dotnet mdsnippets && git diff --exit-code     # when docs changed
dotnet slopwatch analyze --fail-on warning
```

plus the AOT publish of the console sample when JSON or generated code changed (see `AGENTS.md`).
