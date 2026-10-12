# Contributing

Agentic is a runtime library, `src/Agentic`, and the Roslyn generator that powers it, `src/Agentic.Generators`,
shipped together as one package. The rules for working on the code, its style and its tests are in
[`AGENTS.md`](https://github.com/weboost-at/Agentic/blob/main/AGENTS.md); they apply to people and coding agents
alike.

## Build and test

```shell
dotnet tool restore
dotnet build -c Release -warnaserror
dotnet test -c Release --no-build
dotnet run --project samples/Agentic.Samples.Docs -c Release --no-build
```

The build is warning-free with warnings as errors. Generator snapshots live in
`tests/Agentic.Generators.Tests/Snapshots`; a mismatch writes a `.received.txt` to review, and
`ACCEPT_SNAPSHOTS=1` accepts it.

## Where to read on

| Page | Read it when |
|---|---|
| [Architecture](./architecture.md) | You change how the generator reads code, what it writes, or how a run resolves. |
| [Decisions](./decisions.md) | Something looks odd. It is probably deliberate, and the entry says why and what lost. |
| [Status](./status.md) | You look for open work, known gaps, or which test covers a behavior. |
| [Documentation](./documentation.md) | You change a guide page or the code it shows. |
| [Renaming](./renaming.md) | The package, its namespace or its registration call get their final names. |
