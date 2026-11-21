# Contributing to Typeshift

Typeshift is a .NET 11 library, `src/Typeshift`, and the Roslyn generator behind it, `src/Typeshift.Generators`,
shipped as one package.

## Where to start

- [Contributing](https://weboost-at.github.io/Typeshift/contributing/), whose source is `docs/contributing/`: how the
  repository is laid out and which page to read before you change what.
  [Status](https://weboost-at.github.io/Typeshift/contributing/status) lists the open work and the known gaps.
- [AGENTS.md](../AGENTS.md): the rules for code, tests, docs and commits, for people and coding agents alike. Its
  "Start here" table names what to read first for your kind of change.

## Build and test

You need the .NET SDK that `global.json` names, and Node.js 22 for the docs site.

```bash
dotnet tool restore
dotnet build Typeshift.slnx -c Release -warnaserror
dotnet test --solution Typeshift.slnx -c Release --no-build
```

The build stays free of warnings, and the suite passes three times in a row before a change is done. "Build and
test" in AGENTS.md lists the rest: the samples, the docs site and the demos.

## Before you open a pull request

- A change a user can observe has its line in `CHANGELOG.md`, under `## [Unreleased]`.
- The README is the owner's text. Propose a change to it in your pull request's description rather than editing it.
- Report a vulnerability in private, as [SECURITY.md](../SECURITY.md) says, never in a public issue.
