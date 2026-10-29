---
title: Architecture
description: How the generator reads toolsets and agents, what it writes, and how the runtime runs, stores and traces an agent.
---

# Architecture

Typeshift ships as one package with two assemblies. `Typeshift.Generators` reads your toolsets and agents at compile
time, and `Typeshift` runs them. The D-numbers point into [Decisions](./decisions.md).

```text
developer code         compile time                                         runtime
--------------         ------------                                         -------
partial Toolset  --->  ToolsetParser --> ToolsetModel --> ToolsetEmitter -> {Tool}Overrides, Create{Tool}(),
                         |                   |                              a GeneratedToolFunction per tool,
                         |                   v                              a ToolList<T> extension, stamps
                         |              Catalog (+ the references' stamps)
                         |                   |
partial Agent    --->  AgentParser ---> AgentModel --> AgentEmitter ------> Settings, Configure{Tool}(), the
                         |                                                  declaration, AgentHost<T> members
services.AddTypeshift() --> InterceptParser --> InterceptorEmitter -------> AddTypeshift(): the modules
                         |                     ModuleEmitter -------------> a module per assembly
                         +--> TypeshiftAnalyzer: TSH diagnostics at real locations
```

## The runtime library

| Namespace | Holds |
|---|---|
| `Typeshift` | `Toolset` and the tool chain (`ToolBuilder`, the settings builders), `ToolName`, `ToolList<T>`; `Agent` and its chains (`ChatOptionsBuilder`, `ToolsetUse<T>`, `RunOptionsBuilder`), `RunOptions`; `AgentHost<T>`, `IChatClientProvider`, `TypeshiftBuilder`. |
| `Typeshift.Generated` | What only generated code calls, hidden from editors (D10): `GeneratedToolFunction`, `ToolParameter`, `ToolJson`, `AgentDeclaration`, `AgentRuns`, `ModuleRegistration` and the stamps. |
| `Typeshift.Toolsets` | The built-in toolsets (`TodoToolset`, `FileSystemToolset`, `ShellToolset`, `WebToolset`) and what they ask the host for, such as `IFileSystemRoot` (D37). |
| `Microsoft.Extensions.DependencyInjection` | `AddTypeshift()`, which throws unless the generator intercepts it (D1). |

At runtime the declaration chains only return the next builder or record a value, such as `WithDisplayName`, the
chat and run options or `Configure{Tool}()`. Which tools exist, what an agent offers and what may change at runtime
is decided by the generated code alone (D4).

## The generator and the analyzer

`TypeshiftGenerator` is an `IIncrementalGenerator` and `TypeshiftAnalyzer` a `DiagnosticAnalyzer`. Both parse with
the same code. The analyzer passes a `Reporter` that reports, while the generator passes `Reporter.None` and skips
what does not parse (D2).

1. **Discovery** (`Parsing/Discovery.cs`) is attribute-free. The syntax predicate passes classes whose base list
   names `Toolset`, `Agent` or `JsonSerializerContext`, and the semantic check confirms the direct base (D3).
2. **Parsing** (`Parsing/ToolsetParser.cs`) reads the constructors through `IOperation`: each
   `AddTool(...).WithName(...).ReadOnly().WithSettings<T>().Configurable(...)` chain, only directly in a constructor
   and only as one statement (D4). The analyzer receives the constructors as operation blocks, and the generator
   reads them through the semantic model.
3. **Models** hold only strings, bools, enums and `EquatableArray<T>`. They carry no locations, so code that moves
   within a toolset leaves its output cached.
4. **Emitting** (`Emitting/ToolsetEmitter.cs`) writes, for each tool, an overrides record when it has configurable
   settings, a hidden `Create{Tool}` factory and a private `GeneratedToolFunction` subclass. For each toolset it
   writes a `{Toolset}ToolList` extension block with a property per tool, plus a `[GeneratedTool]` stamp per tool
   for agents in other assemblies (D7).
5. **Agents** (`Parsing/AgentParser.cs`) read the chat and run option picks, the toolset uses and their picks. The
   tool list's properties are the generator's own output, so a pick from it does not bind there. The parser walks
   those calls as invalid operations and reads the picks from syntax instead. `Parsing/Catalog.cs` resolves the uses
   against the compilation's toolsets and those the references stamp (`Parsing/ReferenceReader.cs`). An agent's
   output stays cached while what it offers stays the same.
6. **Registration.** `Emitting/ModuleEmitter.cs` writes one module, stamped with `[GeneratedModule]`, per assembly
   that declares toolsets or agents. `Emitting/InterceptorEmitter.cs` intercepts every `AddTypeshift()` call and
   registers the core services, the project's module and its references' modules (D1).

The analyzer checks what spans declarations in a compilation-end action: unique tool names, the references' stamps
included, and references without a module. Calls the generator cannot read (TSH014, TSH015) it checks in an
operation action. `ParameterDocsSuppressor` suppresses CS1573 for the parameters of a tool the model never sees
(D14).

## Tool invocation

1. `FunctionInvokingChatClient` calls `InvokeAsync` with the model's arguments and the services it was built with,
   which are the run's (D25).
2. The generated `InvokeToolAsync` binds each parameter with `GetArgument<T>`. A value that is already `T`, a JSON
   element or JSON text is converted through `JsonTypeInfo<T>`. A missing required argument throws an
   `ArgumentException`, and an absent optional one takes its default. The settings the function was created with,
   the services, the token and the raw arguments pass through and stay out of the schema (D16).
3. The result is serialized through `JsonTypeInfo` into a `JsonElement`.

`ToolJson` builds the schema of each function type once and caches it. That is one
`AIJsonUtilities.CreateJsonSchema` per parameter, with its description and default, nullable references widened to
`[type, "null"]`, the `$ref`s of a recursive type rebased under its parameter, and properties described by the XML
docs the modules registered (D55). For JSON metadata, `ToolJson` asks the modules' JSON contexts first, then
Microsoft.Extensions.AI's defaults, which reflect outside Native AOT (D45).

## A run

1. The generated `RunAsync` passes the input, the settings id (boxed) and the run's `Settings` to `AgentRuns`, which
   calls the host.
2. `AgentHost<T>` resolves the options. It starts from a copy of the agent's chat options and its instructions, and
   applies each settings layer through the generated declaration: the stored settings, then the run's. Each layer
   reaches the configurable options and the run options. Then come the offered tools, created with their layered
   settings (D27), the sub-agents (D33) and the MCP servers' tools (D35).
3. It asks the scope's `IChatClientProvider` for the client of the run's provider and model and builds the pipeline,
   outermost first:
   - the conversation (D46, D50), run middleware (D30), context providers (D31), approval and modes (D24, D52),
     and author names on every message (D26);
   - `FunctionInvokingChatClient`, with the run options, the scope's services, `exit_plan_mode` and the tool
     middleware as its function invoker, built anew for each round with what is left of the run's limit, so one
     client serves concurrent calls;
   - the checkpoints, compaction (D32), and the provider's client, instrumented (D36), behind a wrapper that never
     disposes it (D29).
4. The options are a `ChatOptions` subclass that remembers the run options, so the raw path's client gets them too.

## Storage

`ISettingsStore<TId>` keeps documents by id and kind, as JSON with a version (D38, D41), and
`IConversationStore<TId>` keeps conversation entries. `ITypeshiftStore<TId>` is both in one. `WithStore<T>()` registers
each of them the store implements, so a host may keep settings and conversations in two stores. The library never sees
the id type. The interceptor registers `StoreAccess<TId>` through `AddCoreServices<TId>`, and runs reach it as
`IStoreAccess` with boxed ids (D11). `StoreAccess` serializes and reads the documents, so a store handles strings and
numbers only. `StoreCheck` runs the rules a store must keep against it, from the store's own tests (D58).

The generator writes each agent's `Settings` as an `IStorable<Settings>`, with a kind, a version, `Serialize` and
`Deserialize`. It is an `IJsonStorable<Settings>` too, whose `WriteJson` and `TryReadJson` serve `Serialize` and the
`StorableJsonConverter` that lets `System.Text.Json` speak the stored shape. Tool overrides are `IJsonStorable` as
well, so an agent's settings can delegate to the overrides of a toolset in another assembly (D44).

## Conversations

A run with a conversation id goes through `ConversationChatClient`. It loads the entries from the last summary on
(`LoadConversationSinceCompactionAsync`) and builds the history without unanswered calls and errors. It registers the
run in `LiveRuns`, which allows one per conversation in a process, stores the input, and ends the run with an entry of
its own.

Inside the tool loop, `CheckpointChatClient` stores the steps before each model call, tool results first and then
any steering input it takes in, and stores each response as it arrives. `ConversationRun` holds what one run stored
and where its steering input went. Streaming runs publish their updates through `LivePublisher` to the watchers
that `ConversationTracker.WatchAsync` registered (D47).

## Approval and modes

`ApprovalChatClient` sits above `FunctionInvokingChatClient`. Before the tool loop runs, it binds the host's answers
to the requests the conversation stored and drops what no stored request matches. It also collects the standing
approvals and switches the mode on an approved plan.

After each pass of the tool loop, it answers the requests that rules, standing approvals and auto mode decide, and
sends the tool loop round again. When requests are left, it stores them with its own answers and ends the run. In
plan mode it offers only the read-only tools and `exit_plan_mode`, and appends the planning instruction. `AgentRun`
carries the mode, and the run's ending entry stores it, so the conversation's next run resumes in it (D52). A
summary stores the mode and the standing approvals too, since a later run reads nothing before it (D24, D32).

## Middleware and context

`RunMiddlewareChatClient` sits below the conversation and runs the middleware the run's services resolve, around
each pass of a run. In a streaming run the chain runs alongside the caller's enumeration and hands updates over a
channel. Below it, `ContextChatClient` asks the agent's providers and adds their instructions, messages and tools to
a copy of the options. It marks those messages as stored, so the checkpoints leave them out. Tool middleware is
`FunctionInvokingChatClient`'s function invoker (D30, D31).

## Telemetry and compaction

`ConversationChatClient` opens each run's `invoke_agent` span. `InstrumentedClients` keeps one Microsoft.Extensions.AI
`OpenTelemetryChatClient` per provider and model, which passes each call on to the client its run got, and the tool
loop finds that client's source for its tool spans.

`CompactingChatClient` sits below the checkpoints, so every step is stored before it shrinks the call. `Compaction`
holds one run's state: the cleared results, the summary and the message it kept from, and the estimate's
calibration (D32, D36).

## Sub-agents and handoff

`UseAgent<T>()` and `UseHandoff<T>()` capture a factory of `AgentHost<T>`, seen as `IAgentHost`. `AgentHost` offers
each sub-agent as a `SubAgentFunction`. It runs the other host with the caller's settings id, mode and standing
approvals, or, in the background, starts it on `ConversationRun`, whose answers join the run through the steering
queue.

`ConversationChatClient` declares the handoff tools when the run has a conversation. When the tool loop passes a
handoff call out, it continues the run with the agent handed to, and it sends the conversation's next run to the
agent that answered last (D33, D34).

## MCP and the built-in toolsets

When `AgentHost` resolves a run's options, it asks the host's `IMcpToolProvider` for each server's tools. It offers
them after the agent's own, marked for plan mode or wrapped for approval (D35). The library's own generator compiles
the built-in toolsets in `Toolsets/` and writes no id-type aliases for them, and consumers register the library's
module like any reference's (D37).

## Related

- [Decisions](./decisions.md): why each part is shaped the way it is, by D-number.
- [How a run works](../guide/running-agents.md#how-a-run-works): the run's steps as a user sees them.
- [Generated members](../guide/generated-members.md): what the emitters write, member by member.
