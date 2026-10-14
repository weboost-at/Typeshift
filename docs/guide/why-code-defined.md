# Why code-defined

Typeshift declares toolsets and agents in C# constructors and reads them at compile time. The generator writes what
would otherwise take reflection: a function per tool, a settings record per agent, typed members to run it, one
registration call. At runtime the declarations do nothing; the generated code is the one truth about which tools
exist, what an agent offers and what may change.

## Mistakes fail the build

Because the generator reads every declaration, it can refuse the wrong ones. Including a tool of another toolset or
configuring a tool the agent does not offer does not compile, since the generated member it would need does not
exist. Everything else the generator cannot read is a [diagnostic](./diagnostics.md) at the line that caused it,
reported by an analyzer, so `#pragma` and `.editorconfig` reach it. Generated code never fails with a compiler error
of its own.

The price is a shape the generator can read: one chain per declaration, directly in the constructor, never a builder
inside a lambda. What you read in the constructor is what the generator saw.

## Tools are static methods

A tool is a static method group. The generated function calls it directly and binds the model's arguments through
`JsonTypeInfo`, so nothing reflects over your types and the host publishes with Native AOT. The method's name gives
the tool's name, its XML docs the descriptions. The tool takes what the model must not choose as parameters the
schema leaves out: its settings, the run's `IServiceProvider`, the token. Services come through the run's scope, so
a request's customer reaches the tool without an injection model of its own.

## Settings are a contract

Only members the code marks configurable may change at runtime, the model included. Each agent gets a `Settings`
record holding exactly those, which a settings form or an API binds to and a store keeps under a stable kind, with a
version and a revision. A customer's settings can never reach a member the code did not open, because the record has
no member to hold it. How the layers combine is in [settings layering](./settings-layering.md).

## The host owns ids and storage

Settings ids and conversation ids are the host's, of the type it chooses; Typeshift never creates one. The host
implements one store, and without one every call that would persist throws, naming the registration, rather than
storing nothing.

## Microsoft.Extensions.AI throughout

Runs return `ChatResponse`, `ChatResponse<T>` and `ChatResponseUpdate`; approval uses `ToolApprovalRequestContent` and
`ToolApprovalResponseContent`; any `IChatClient` serves a model, and the raw path hands you the client itself. There is
no second vocabulary for what Microsoft.Extensions.AI already names.

## What it gives up

- **Agents are not data.** An agent cannot be defined or stored at runtime; its shape changes with a build. Stored
  settings change only what its code opens.
- **No inheritance.** Toolsets and agents derive from `Toolset` and `Agent` directly; share declarations through
  static methods and constants.
- **No lambdas or instance methods as tools.** Generated code cannot call them without reflection.
- **The package in every project that declares toolsets or agents**, since NuGet does not pass a generator on.

## Typeshift vs AIFunctionFactory and Microsoft Agent Framework

**`AIFunctionFactory`** builds an `AIFunction` from any delegate at runtime, through reflection. Typeshift writes the
function at compile time from a static method: the schema matches `AIFunctionFactory`'s for the same method, but
arguments bind without reflection, settings, services and tokens stay out of the schema, and names are snake_case. A
function from `AIFunctionFactory` still fits an agent: `WithChatOptions().WithTools(...)` takes any `AITool`.

**Microsoft Agent Framework** builds agents on the same Microsoft.Extensions.AI abstractions, assembled at runtime.
Typeshift takes no dependency on it, and took its harness capabilities: tool approval, middleware, compaction,
structured output, sub-agents and handoffs, MCP tools, context providers and OpenTelemetry. Where the shapes differ,
Typeshift chose fewer concepts:

| Concern | Typeshift |
|---|---|
| Middleware | One interface serves streaming and non-streaming runs, where Agent Framework takes a delegate for each. |
| Compaction | Two steps inside the tool loop, clearing old tool results then summarizing, in place of a strategy pipeline with trigger combinators. |
| Background sub-agents | One tool and an answer injected into the run, in place of six background-task tools. |
| To-dos | One tool replacing the list, kept in the conversation, in place of five tools with state in the session. |
| Plan mode | A run option the host switches, where mode tools would let the model leave plan mode unasked. |

What Typeshift adds is the compile-time half: the checked declarations, the `Settings` contract per agent with its
stored layers, and typed members to run each agent.
