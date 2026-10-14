# Typeshift

Code-defined agents and toolsets for .NET 10 on Microsoft.Extensions.AI. A source generator turns them into
reflection-free `AIFunction`s, a typed settings record per agent, and one `services.AddTypeshift()` call.

**Alpha:** the API will change between releases.

## Install

Add the package to every project that declares toolsets or agents, or calls `AddTypeshift()`: it carries the source
generator, and NuGet does not pass generators on to referencing projects. Requires the .NET 10 SDK and C# 14.

Projects that declare toolsets also need `<GenerateDocumentationFile>true</GenerateDocumentationFile>`: tools take
their descriptions from XML docs, which the compiler reads only then, and without it the build warns with
[TSH011](https://weboost-at.github.io/Typeshift/guide/diagnostics#tsh011).

## What you get

- Tools from static methods, with JSON schemas from their parameters and XML docs.
- Tool settings that layer code defaults, the agent's code, its stored settings and the run.
- A generated `Settings` record per agent, holding exactly what may change at runtime.
- One store interface, `ITypeshiftStore<TId>`, with the id type you already use; without a store, agents run from code.
- Runs that return Microsoft.Extensions.AI's `ChatResponse`, streaming and structured output included.
- Conversations stored step by step: resume after a crash, steer a run going on, watch it live.
- Tool approval with standing approvals and rules, and built-in plan and auto modes.
- Middleware around runs and tool calls, and context providers that add to every run.
- Long runs that stay within their context: compaction before every model call, with summaries stored.
- OpenTelemetry traces and metrics after the GenAI conventions, always on.
- Compile-time diagnostics instead of runtime surprises.

## Learn more

- [Documentation](https://weboost-at.github.io/Typeshift/)
- [Toolsets](https://weboost-at.github.io/Typeshift/guide/toolsets)
- [Agents](https://weboost-at.github.io/Typeshift/guide/agents)
