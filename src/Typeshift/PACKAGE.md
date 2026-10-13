# Typeshift

Code-defined agents and toolsets for .NET 10 on Microsoft.Extensions.AI. A source generator turns them into
reflection-free `AIFunction`s, a typed settings record per agent, and one `services.AddTypeshift()` call. Everything
runs under Native AOT.

**Alpha:** the API will change between releases.

## Install

Add the package to every project that declares toolsets or agents, or calls `AddTypeshift()`: it carries the source
generator, and NuGet does not pass generators on to referencing projects. Requires the .NET 10 SDK and C# 14.

Projects that declare toolsets also need `<GenerateDocumentationFile>true</GenerateDocumentationFile>`: tools take
their descriptions from XML docs, which the compiler reads only then, and without it the build warns with
[TSH010](https://weboost-at.github.io/Typeshift/guide/diagnostics#tsh010).

## What you get

- Tools from static methods, with JSON schemas from their parameters and XML docs.
- Tool settings that layer code defaults, stored defaults, the agent's code, its stored settings and the run.
- A generated `Settings` record per agent, holding exactly what may change at runtime.
- One store interface, `ITypeshiftStore<TId>`, with the id type you already use; without a store, agents run from code.
- Conversation tracking, streaming included.
- Compile-time diagnostics instead of runtime surprises.

## Learn more

- [Documentation](https://weboost-at.github.io/Typeshift/)
- [Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started)
- [Changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md)
- [Source](https://github.com/weboost-at/Typeshift)
