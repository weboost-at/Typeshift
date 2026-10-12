# Agentic

Code-defined agents and toolsets for .NET 10 on Microsoft.Extensions.AI. A source generator turns them into
reflection-free `AIFunction`s, a typed settings record per agent, and one `services.AddAgentic()` call. Everything runs
under Native AOT.

**Alpha:** the API will change between releases.

## Install

Add the package to every project that declares toolsets or agents, or calls `AddAgentic()`: it carries the source
generator, and NuGet does not pass generators on to referencing projects. Requires the .NET 10 SDK and C# 14.

## What you get

- Tools from static methods, with JSON schemas from their parameters and XML docs.
- Tool settings that layer code defaults, stored defaults, the agent's code, its stored settings and the run.
- A generated `Settings` record per agent, holding exactly what may change at runtime.
- One store interface, `IAgenticStore<TId>`, with the id type you already use; without a store, agents run from code.
- Conversation tracking, streaming included.
- Compile-time diagnostics instead of runtime surprises.

## Learn more

- [Documentation](https://weboost-at.github.io/Agentic/)
- [Getting started](https://weboost-at.github.io/Agentic/guide/getting-started)
- [Changelog](https://github.com/weboost-at/Agentic/blob/main/CHANGELOG.md)
- [Source](https://github.com/weboost-at/Agentic)
