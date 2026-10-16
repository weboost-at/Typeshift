---
title: Comparison
description: How Typeshift compares with Microsoft.Extensions.AI's AIFunctionFactory and with Microsoft Agent Framework, and what it gives up.
---

# Comparison

Typeshift builds on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai), like
`AIFunctionFactory` and Microsoft Agent Framework do. The difference is when the work happens. They assemble tools and
agents at runtime. Typeshift reads them from your code at compile time, which buys build errors, tools without
reflection and a typed settings contract per agent, and costs the freedom to change an agent without a build.

::: tip The short version

- Runs speak Microsoft.Extensions.AI's types. Any `IChatClient` serves a model.
- Against `AIFunctionFactory`: the same schema, no reflection, settings and services out of the schema, snake_case
  names.
- Against Agent Framework: the same harness capabilities, with fewer concepts, plus checked declarations and a
  `Settings` record per agent.
- What it gives up: agents are not data, there is no inheritance, and tools are static methods.

:::

## Microsoft.Extensions.AI throughout

Typeshift adds no second vocabulary for what Microsoft.Extensions.AI already names:

- Runs return `ChatResponse`, `ChatResponse<T>` and `ChatResponseUpdate`.
- Approval uses `ToolApprovalRequestContent` and `ToolApprovalResponseContent`.
- Any `IChatClient` serves a model, and the [raw path](./running-agents.md#the-raw-path) hands you the client itself.

## Compared with AIFunctionFactory

`AIFunctionFactory` builds an `AIFunction` from any delegate at runtime, through reflection. Typeshift writes the
function at compile time from a static method. For the same method, the schema matches `AIFunctionFactory`'s. What
differs:

| Concern | `AIFunctionFactory` | Typeshift |
|---|---|---|
| Built | At runtime, through reflection | At compile time, by the source generator |
| Tools from | Any delegate | Static methods |
| Arguments bind | Through reflection | Through generated code and `JsonTypeInfo`, ready for [Native AOT](./native-aot.md) |
| Left out of the schema | `IServiceProvider`, `CancellationToken`, `AIFunctionArguments` | The same, plus the tool's [settings](./tool-settings.md) |
| Names | The method's name | The method's name in snake_case |

A function from `AIFunctionFactory` still fits an agent: `WithChatOptions().WithTools(...)` takes any `AITool`.

## Compared with Microsoft Agent Framework

Microsoft Agent Framework builds agents on the same Microsoft.Extensions.AI abstractions, assembled at runtime.
Typeshift takes no dependency on it. It adopts Agent Framework's [harness
capabilities](https://learn.microsoft.com/en-us/agent-framework/concepts/harness?pivots=programming-language-csharp#harness-capability-matrix):
tool approval, middleware, compaction, structured output, sub-agents and handoffs, MCP tools, context providers and
OpenTelemetry. Where the shapes differ, Typeshift chose fewer concepts:

| Concern | Typeshift |
|---|---|
| Middleware | One interface serves streaming and non-streaming runs, where Agent Framework takes a delegate for each. |
| Compaction | Two steps inside the tool loop, clearing old tool results then summarizing, in place of a strategy pipeline with trigger combinators. |
| Background sub-agents | One tool and an answer injected into the run, in place of six background-task tools. |
| To-dos | One tool replacing the list, kept in the conversation, in place of five tools with state in the session. |
| Plan mode | A run option the host switches, where mode tools would let the model leave plan mode unasked. |

What Typeshift adds is the compile-time half. A mistake in a declaration fails the build at the line that caused it.
Each agent gets a `Settings` record with exactly what its code marks configurable, and typed members to run it.

## What it gives up

- **Agents are not data.** You cannot define or store an agent at runtime. Its shape changes with a build, and stored
  settings change only what its code opens.
- **One chain per declaration.** Each declaration is one chain, directly in the constructor, so the generator can read
  it ([TSH014](./diagnostics.md#tsh014), [TSH015](./diagnostics.md#tsh015)).
- **No inheritance.** Toolsets and agents derive from `Toolset` and `Agent` directly
  ([TSH017](./diagnostics.md#tsh017)). Share declarations through static methods and constants.
- **No lambdas or instance methods as tools.** Generated code cannot call them without reflection
  ([TSH003](./diagnostics.md#tsh003)).
- **The package in every project that declares toolsets or agents.** NuGet does not pass a generator on
  ([TSH020](./diagnostics.md#tsh020)).

## Related

- [Getting started](./getting-started.md): how the pieces fit, and a working agent.
- [Settings layering](./settings-layering.md): how the settings contract resolves in a run.
- [Diagnostics](./diagnostics.md): every mistake the generator reports.
- [Decisions](../contributing/decisions.md): the same choices from the code's side.
