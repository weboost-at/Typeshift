---
title: Contributing
description: How the repository is laid out, how to build and test it, and which page to read before you change what.
---

# Contributing

Typeshift is a runtime library, `src/Typeshift`, and the Roslyn generator that powers it, `src/Typeshift.Generators`,
shipped together as one package. The rules for working on the code, its style and its tests are in
[`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md). They apply to people and coding agents
alike.

::: tip The short version

- Build warning-free, run the tests three times, run the docs program. The commands are below.
- Something looks odd? It is probably deliberate: [Decisions](./decisions.md) says why.
- Open work and known gaps live in [Status](./status.md), never as TODOs in code.
- A change people can see changes the docs in the same commit: see [Documentation](./documentation.md).

:::

## Build and test

```bash
dotnet tool restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet run --project samples/Typeshift.Samples.Docs -c Release --no-build
```

The build is warning-free with warnings as errors. Generator snapshots live in
`tests/Typeshift.Generators.Tests/Snapshots`. A mismatch writes a `.received.txt` to review, and
`ACCEPT_SNAPSHOTS=1` accepts it.

## Working with coding agents

`AGENTS.md` is written for coding agents as much as for people. It holds a table from task to the docs to read and
the playbook to follow, the traps that cost debugging sessions before, the guard tests that enforce the repository's
rules, and the fact map of the docs. The playbooks live in `.claude/skills/`, where Claude Code loads them as skills;
other agents read them as Markdown.

Code comments of the form `// D29: ...` cite the [decision](./decisions.md) behind code that looks odd. A test fails
when a cited decision is withdrawn.

## Where to read on

| Page | Read it when |
|---|---|
| [Architecture](./architecture.md) | You change how the generator reads code, what it writes, or how a run resolves. |
| [Decisions](./decisions.md) | Something looks odd. It is probably deliberate, and the entry says why and what lost. |
| [Status](./status.md) | You look for open work, known gaps, or which test covers a behavior. |
| [Releasing](./releasing.md) | You publish a version to NuGet. |
| [Documentation](./documentation.md) | You change a guide page or the code it shows. |
| [Writing style](./writing.md) | You write or review a page. |

## Related

- [Architecture](./architecture.md): how the generator and the runtime fit together.
- [Documentation](./documentation.md): snippets, checks and templates.
- [Overview](../guide/index.md): the library as its users see it.
