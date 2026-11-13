# Typeshift

Code-defined agents and toolsets for .NET 11, built on Microsoft.Extensions.AI. Build AI agents with just a few lines
of code, and let the compiler catch your mistakes.

**Alpha:** the API may change between releases.

## Quick start

```bash
dotnet add package Typeshift --prerelease
```

1. **Write a toolset.** A `partial` class deriving from `Toolset` adds each tool, a static method, with `AddTool(...)`
   in its constructor. The tool's XML docs tell the model what it does, and the package turns XML docs on for you.
2. **Write an agent.** A `partial` class deriving from `Agent` picks its model with `WithChatOptions().WithModel(...)`,
   its instructions and its toolsets in its constructor.
3. **Run it.** Without a container, `TypeshiftRuntime.Create(client)` takes any `IChatClient`, and
   `runtime.Agent<TAgent>()` gives you the agent's host. With one, `services.AddTypeshift()` registers every toolset
   and agent, and you inject `AgentHost<TAgent>`.

Typeshift keeps settings and conversations in memory until you register a store, so a first app needs nothing else.
The [README](https://github.com/weboost-at/Typeshift#quick-start) shows the code, and
[Getting started](https://weboost-at.github.io/Typeshift/guide/getting-started) explains every line.

Add the package to every project that declares toolsets or agents, or calls `AddTypeshift()` or
`TypeshiftRuntime.Create(...)`: the package carries the source generator, and a direct reference is the one setup
that always passes it on.

## What makes Typeshift different

- **Catch mistakes at compile time.** Configure a tool the agent doesn't offer, and the build fails. The analyzer
  reports the rest, each [diagnostic](https://weboost-at.github.io/Typeshift/guide/diagnostics) with how to fix it.
- **Store settings per customer.** Each agent gets a typed `Settings` record that holds only what you mark
  `Configurable`, kept under the ids you already use.
- **Stay on Microsoft.Extensions.AI.** Any `IChatClient` serves a model, tools are `AIFunction`s, and a run returns a
  `ChatResponse`.
- **Publish with Native AOT.** The generated code never reflects over your types, and the analyzer names each tool
  type that still needs JSON metadata.

## Learn more

- [Documentation](https://weboost-at.github.io/Typeshift/)
- [Source and issues](https://github.com/weboost-at/Typeshift)
- [Changelog](https://github.com/weboost-at/Typeshift/blob/main/CHANGELOG.md)
