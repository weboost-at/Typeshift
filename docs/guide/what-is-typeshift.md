---
title: What is Typeshift?
description: What Typeshift is for, what it builds on, how it compares with writing the same on Microsoft.Extensions.AI by hand, and what it leaves to you.
---

# What is Typeshift?

Typeshift is a .NET library for AI agents that you define in code. You write each tool as a static method with XML
docs and each agent as a small class that names its model, its instructions and its tools. A source generator turns
them into what Microsoft.Extensions.AI runs, so a mistake such as configuring a tool the agent doesn't offer fails
the build, and an analyzer reports others while you type.

## Why agents in code

An agent is code you review, version and test like the rest of your app. Its tools are methods the compiler checks,
and what may change at runtime is a typed `Settings` record that Typeshift generates from what you mark configurable.
Your customers can store their own instructions or a lower search limit, and the code decides which values they may
touch.

The cost is that an agent cannot be created at runtime, for example from a prompt your users write. Stored settings and
[context providers](./middleware.md#context-providers) cover most of what such agents are used for: instructions per
customer, memory, documents and tools added to a run.

## What it builds on

Typeshift runs on [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A model is
any `IChatClient`, from OpenAI, Anthropic, a local server or your own wrapper. Tools are `AIFunction`s, a run's options
are `ChatOptions`, and the tool loop is `FunctionInvokingChatClient`. What a run returns is a `ChatResponse`. Spans and
metrics follow the OpenTelemetry conventions for generative AI, and an app can publish with Native AOT.

## Compared with writing it by hand

Everything Typeshift does, you could write on Microsoft.Extensions.AI yourself. This is what you would write:

| By hand | With Typeshift |
|---|---|
| An `AIFunctionFactory.Create` call per tool, and its description kept in step with the method | A static method with XML docs; the schema and description are generated |
| Values the model must not choose, such as a search limit, passed around by hand | A settings record the tool receives and the model never sees, layered from code, storage and the run |
| A `ChatOptions` per agent, and code that picks the client for its provider | `WithChatOptions().WithModel(...)` and a registered `IChatClient` |
| Storing the history between requests, and resuming after a crash | A conversation id on the run |
| Asking a person before a call that closes a ticket | `RequiresApproval()`, approval rules, and plan and auto modes |
| Agents that call each other, or hand a conversation on | `UseAgent<T>()` and `UseHandoff<T>()` |
| Registration for each of the above | `services.AddTypeshift()` |

## How the pieces fit

```mermaid
flowchart LR
  subgraph compile [Compile time]
    toolset[Toolset] --> generator[Source generator]
    agent[Agent] --> generator
    generator --> functions[An AIFunction per tool]
    generator --> settings[A Settings record per agent]
    generator --> registration["AddTypeshift()"]
  end
  subgraph runtime [Runtime]
    host["AgentHost&lt;TAgent&gt;"] --> client[The provider's IChatClient]
    host --> store[Your store]
  end
  registration --> host
```

`AddTypeshift()` registers an `AgentHost<TAgent>` per agent. The host resolves the settings, calls the model, runs the
tools and stores the conversation. [How Typeshift reads your code](./how-typeshift-reads-your-code.md) explains the
compile-time half, and [How a run works](./how-a-run-works.md) the steps of a run.

| Piece | What it is | Read |
|---|---|---|
| Toolset | A `partial` class deriving from `Toolset` that declares a group of related tools | [Toolsets](./toolsets.md) |
| Tool settings | A record of values a tool needs that the model must not choose | [Tool settings](./tool-settings.md) |
| Agent | A `partial` class deriving from `Agent`, with chat options, instructions and tools | [Agents](./defining-agents.md) |
| Agent host | `AgentHost<TAgent>`, which runs one agent in a scope: the one it was resolved from, or the one the runtime created for it | [Using an agent](./using-an-agent.md) |
| Store | Your `ITypeshiftStore<TId>`, or an `ISettingsStore<TId>` and an `IConversationStore<TId>`, which keep settings and conversations under ids of your type; an `IAgentSettingsStore` for each agent's own settings | [Settings and stores](./settings-and-stores.md) |

The [glossary](./glossary.md) defines every other term.

## What Typeshift leaves to you

- **The model.** You register the `IChatClient` of your provider; Typeshift ships no provider SDK.
- **Your app.** Typeshift runs inside ASP.NET Core, a worker or a console app, and serves no endpoints of its own.
  [Serving a chat endpoint](./chat-endpoint.md) shows the few a chat UI needs.
- **Your database.** `InMemoryTypeshiftStore<TId>` serves tests and samples. For settings and conversations that must
  survive a restart, you write a [store](./implementing-a-store.md) over your own database.
- **Search over your documents.** A [context provider](./middleware.md#context-providers) adds what your own search
  returns to a run.

## Related

- [Getting started](./getting-started.md): a first toolset, agent and run, in four steps.
- [Agents](./defining-agents.md): everything an agent's constructor can declare.
- [How Typeshift reads your code](./how-typeshift-reads-your-code.md): what is read when you build, and what that gives you.
- [Glossary](./glossary.md): the words these docs use.
