---
title: Why code-defined
description: Why Typeshift declares toolsets and agents in C# and reads them at compile time, what that gives you and what it costs.
---

# Why code-defined

In Typeshift, you declare toolsets and agents in C# constructors, and a source generator reads them at compile time. It
writes what would otherwise take reflection: a function per tool, a settings record per agent, typed members to run it
and one registration call. That turns mistakes into build errors and gives every agent a typed settings contract. The
price is flexibility: an agent is code, and its shape changes only with a build.

::: tip The short version

- The generator reads every declaration. A mistake fails the build at the line that caused it, never inside generated
  code.
- Tools are static methods. Generated code calls them directly, so nothing reflects over your types.
- Each agent's `Settings` record holds exactly what the code marks configurable. Nothing else can change at runtime.
- You own the ids and the storage, and runs speak Microsoft.Extensions.AI's types.
- The cost: agents are not data, there is no inheritance, and every declaring project needs the package.

:::

## Mistakes fail the build

The generator reads every declaration, so it can refuse the wrong ones. Some mistakes do not compile at all. Including a
tool of another toolset, or configuring a tool the agent does not offer, needs a generated member that does not exist.

Everything else the generator cannot read is a [diagnostic](./diagnostics.md) at the line that caused it. An analyzer
reports it, so `#pragma` and `.editorconfig` reach it. Generated code never fails with a compiler error of its own.

The price is a shape the generator can read. Each declaration is one chain, directly in the constructor, never a builder
inside a lambda. In return, what you read in the constructor is what the generator saw. At runtime the declarations do
nothing: the generated code is the one truth about which tools exist, what an agent offers and what may change.

## Tools are static methods

A tool is a static method group. The generated function calls it directly and binds the model's arguments through
`JsonTypeInfo`. So nothing reflects over your types, and your app can publish with [Native AOT](./native-aot.md). The
method's name gives the tool's name, and its XML docs give the descriptions.

A tool takes what the model must not choose as parameters the schema leaves out: its settings, the run's
`IServiceProvider` and the token. Services come from the run's scope. So a request's services, such as the current
customer, reach the tool without an injection model of Typeshift's own. See [Toolsets](./toolsets.md#parameters).

## Settings are a contract

Only what the code marks configurable may change at runtime, and that includes the model. Each agent gets a `Settings`
record that holds exactly those members. A settings form or an API binds to it. A store keeps it under a stable kind,
with a version and a revision.

A customer's settings can never reach a member the code did not open, because the record has no member to hold it.
[Settings layering](./settings-layering.md) shows how the stored settings combine with the code.

## You own ids and storage

Settings ids and conversation ids belong to your app, in the [type it chooses](./registration.md#the-id-type). Typeshift
never creates one.

Your app implements one store. Without a store, every call that would persist throws and names the registration it
needs, rather than storing nothing. See [Settings and stores](./settings-and-stores.md).

## Microsoft.Extensions.AI throughout

Typeshift adds no second vocabulary for what
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) already names:

- Runs return `ChatResponse`, `ChatResponse<T>` and `ChatResponseUpdate`.
- Approval uses `ToolApprovalRequestContent` and `ToolApprovalResponseContent`.
- Any `IChatClient` serves a model, and the [raw path](./running-agents.md#the-raw-path) hands you the client itself.

## What it gives up

- **Agents are not data.** You cannot define or store an agent at runtime. Its shape changes with a build, and stored
  settings change only what its code opens.
- **No inheritance.** Toolsets and agents derive from `Toolset` and `Agent` directly
  ([TSH017](./diagnostics.md#tsh017)). Share declarations through static methods and constants.
- **No lambdas or instance methods as tools.** Generated code cannot call them without reflection
  ([TSH003](./diagnostics.md#tsh003)).
- **The package in every project that declares toolsets or agents.** NuGet does not pass a generator on
  ([TSH020](./diagnostics.md#tsh020)).

## Compared with AIFunctionFactory

Microsoft.Extensions.AI's `AIFunctionFactory` builds an `AIFunction` from any delegate at runtime, through reflection.
Typeshift writes the function at compile time from a static method. For the same method, the schema matches
`AIFunctionFactory`'s. What differs:

- Arguments bind without reflection.
- Settings, services and tokens stay out of the schema.
- Names are snake_case.

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

What Typeshift adds is the compile-time half: the checked declarations, the `Settings` contract per agent with its
stored layers, and typed members to run each agent.

## Related

- [Getting started](./getting-started.md): the idea at work in a small agent.
- [Settings layering](./settings-layering.md): how the settings contract resolves in a run.
- [Diagnostics](./diagnostics.md): every mistake the generator reports.
- [Decisions](../contributing/decisions.md): the same choices from the code's side.
