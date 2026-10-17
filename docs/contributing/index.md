---
title: Contributing
description: How the repository is laid out, how to build and test it, and which page to read before you change what.
---

# Contributing

Typeshift is a runtime library, `src/Typeshift`, and the Roslyn generator behind it, `src/Typeshift.Generators`, shipped
as one package. [`AGENTS.md`](https://github.com/weboost-at/Typeshift/blob/main/AGENTS.md) holds the rules for code,
style and tests, for people and coding agents alike.

## Build and test

```bash
dotnet tool restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet run --project samples/Typeshift.Samples.Docs -c Release --no-build
```

The build treats warnings as errors and must stay free of them. Run the tests three times before you call a change
done. Generator snapshots live in `tests/Typeshift.Generators.Tests/Snapshots`: a mismatch writes a `.received.txt`
for you to review, and rerunning with `ACCEPT_SNAPSHOTS=1` accepts it. Open work goes into [Status](./status.md),
never into a TODO in code, and a change people can see changes the docs in the same commit.

## Working with coding agents

`AGENTS.md` is written for coding agents as much as for people. It maps each task to the docs to read and the
playbook to follow, and lists the traps that once cost a debugging session, the guard tests and the docs' fact map.
The playbooks live in `.claude/skills/`. Claude Code loads them as skills, and other agents read them as Markdown.

A comment such as `// D29: ...` cites the [decision](./decisions.md) behind the code next to it, and a test fails
once that decision is withdrawn.

## Where to read on

| Page | Read it when |
|---|---|
| [Architecture](./architecture.md) | You change how the generator reads code, what it writes, or how a run resolves. |
| [Decisions](./decisions.md) | Something looks odd. The entry says why it is so and what lost. |
| [Status](./status.md) | You look for open work, known gaps, or which test covers a behavior. |
| [Releasing](./releasing.md) | You publish a version to NuGet. |
| [Writing the docs](./writing.md) | You change a guide page or the code it shows, or review a page. |

## Related

- [Architecture](./architecture.md): how the generator and the runtime fit together.
- [Writing the docs](./writing.md): the voice, snippets, checks and templates.
- [Getting started](../guide/getting-started.md): the library as its users see it.
