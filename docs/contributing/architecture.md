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
| `Typeshift` | `Toolset` and the tool chain (`ToolBuilder`, the settings builders), `ToolName`, `ToolList<T>`; `Agent` and its chains (`ChatOptionsBuilder`, `ToolsetUse<T>`, `RunOptionsBuilder`), `RunOptions`; `TypeshiftRuntime`, `AgentHost<T>`, `IChatClientProvider<TProviderId>`, `TypeshiftBuilder`. |
| `Typeshift.Generated` | What only generated code calls, hidden from editors (D10): `GeneratedToolFunction`, `ToolParameter`, `ToolJson`, `AgentDeclaration`, `AgentRuns`, `ModuleRegistration` and the stamps. |
| `Microsoft.Extensions.DependencyInjection` | `AddTypeshift()`, which throws unless the generator intercepts it (D1). |

At runtime the declaration chains only return the next builder or record a value, such as `WithDisplayName`, the
chat and run options or `Configure{Tool}()`. Which tools exist, what an agent offers and what may change at runtime
is decided by the generated code alone (D4).

`TypeshiftRuntime` is a singleton over the root provider, which `AddCoreServices` registers. `Agent<TAgent>()`
creates an async scope and a host that owns it, and disposing the host disposes the scope; a host the container
resolved owns nothing. The runtime that `Create` returns owns the container it built, so disposing the runtime
disposes the container. A runtime an app's own container created leaves that container alone (D76).

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
3. **Models** are records that hold only strings, bools, ints, enums, other models and `EquatableArray<T>` of
   those, which `ModelShapeTests` checks. They carry no locations, so code that moves within a toolset leaves
   its output cached.
4. **Emitting** (`Emitting/ToolsetEmitter.cs`) writes, for each tool, an overrides record when it has configurable
   settings, a hidden `Create{Tool}` factory and a private `GeneratedToolFunction` subclass. For each toolset it
   writes an extension block on `ToolList<T>` with a property per tool, in a file of its own and the global
   namespace (D12), plus a `[GeneratedTool]` stamp per tool
   for agents in other assemblies (D7).
5. **Agents** (`Parsing/AgentParser.cs`) read the chat and run option picks, the type of the provider `WithModel`
   names, which types the provider members of the agent's `Settings` (D77), the toolset uses and their picks, and
   the agent's own tools, which `ToolsetParser.ParseTools` reads as it reads a toolset's (D57). The tool list's
   properties are the generator's own output, so a pick from it does not bind there. The parser walks those calls as
   invalid operations and reads the picks from syntax instead. `Parsing/Catalog.cs` resolves the uses against the
   compilation's toolsets and those the references stamp (`Parsing/ReferenceReader.cs`), then adds the agent's own
   tools, whose members `AgentEmitter` writes into the agent through `ToolsetEmitter.WriteTools`. An agent's output
   stays cached while what it offers stays the same.
6. **Registration.** `Emitting/ModuleEmitter.cs` writes one module, stamped with `[GeneratedModule]`, per assembly
   that declares toolsets or agents or holds a JSON context or a `[SchemaAnnotations]` class. The package adds that class to each
   project with XML docs on, so by default every project gets a module (D55). `Emitting/InterceptorEmitter.cs`
   intercepts every `AddTypeshift()` call and registers the core services, the project's module and its references'
   modules (D1). It intercepts every `TypeshiftRuntime.Create` call too, which builds a container of the same
   registration (D75).

The analyzer checks what spans declarations in a compilation-end action: unique tool names, the references' stamps
included, and references without a module. It also compares what it parsed with what the generator wrote, by the
stamps of the generated code, since the generator's compilation lacks every generator's output (TSH032, D2). A
descriptor that only this action reports carries the `CompilationEnd` tag, which tells an editor that it shows on a
build. Calls the generator cannot read (TSH014, TSH015) or intercept (TSH040) it checks in operation actions, where
it also names the overloads behind an `AddTool` the compiler could not bind (TSH003).

`ParameterDocsSuppressor` suppresses CS1573 for the parameters of a tool the model never sees (D14). A suppressor
gets no operations, so it finds the `AddTool` calls of the compilation by their name and binds the method each one
names, wherever that method lives.

## Tool invocation

1. `FunctionInvokingChatClient` calls `InvokeAsync` with the model's arguments and the services it was built with,
   which are the run's (D25).
2. The generated `InvokeToolAsync` binds each parameter with `GetArgument<T>`. A value that is already `T`, a JSON
   element or JSON text is converted through `JsonTypeInfo<T>`. A required argument that is missing, or null where
   the parameter takes none, throws a `ToolException`, as does JSON that does not fit, each naming the tool and the
   argument (D53). An absent optional argument takes its default. The settings the function was created with, the
   services, the token and the raw arguments pass through and stay out of the schema (D16).
3. The result is serialized through `JsonTypeInfo` into a `JsonElement`. A stream is read to its end first, by the
   library's `GetResultAsync`, which stops at the call's token, and serialized as the list of its items (D45).

`ToolJson` builds the schema of each function type once and caches it. That is one
`AIJsonUtilities.CreateJsonSchema` per parameter, with its description and default, nullable references widened to
`[type, "null"]`, the `$ref`s of a recursive type rebased under its parameter, and properties described by the XML
docs the modules registered (D55). For JSON metadata, `ToolJson` asks the modules' JSON contexts first, then
Microsoft.Extensions.AI's defaults, which reflect outside Native AOT (D45). Every schema it builds names a union's
type on each case, and its options pick a union's case by the JSON token, then by an object's properties
(D62). `RunAsync<T>` asks it for the answer's response format, built as a parameter's schema is and held under `data`
unless it is an object of properties.

## A run

1. The generated `RunAsync` passes the input, the settings id (boxed) and the run's `Settings` to `AgentRuns`, which
   calls the host. `AnswerAsync` passes your app's answers the same way; the host records them and then resumes
   (D80).
2. `AgentHost<T>` takes the run's conversation before anything else (`RunStart`): it claims it (D50), loads it
   from its last summary, saves a mode the run's settings name (D52) and learns which agent answers (D34). When
   another agent does, the host passes the run on to that agent's host with a `RunHandover` that holds the claim
   and the loaded entries, and resolves nothing itself. A call on the raw path takes its conversation the same
   way, inside the client.
3. The host resolves the options. It starts from a copy of the agent's chat options and its instructions, and
   applies each settings layer through the generated declaration: the agent's own stored settings, those under the
   settings id, then the run's. Each layer
   reaches the configurable options and the run options. Then come the offered tools, created with their layered
   settings (D27), the sub-agents (D33) and the MCP servers' tools (D35).
4. It takes the client of the run's provider and model from the scope's `IChatClientProvider<T>` of the provider's
   type (D77), an `IChatClient` keyed by the provider, or the one registered for every agent (D63), and builds the
   pipeline, outermost first:
   - the conversation (D46, D50), which also settles the decisions that wait (D80), run middleware (D30), context
     providers (D31), the mode (D52), and author names on every message (D26);
   - `ToolLoop`, a `FunctionInvokingChatClient` that writes one fixed sentence as the result of a call whose
     exception is no `ToolException` (D53), with the run options, the scope's services, `exit_plan_mode` and, as its
     function invoker, the tool gate with the tool middleware behind it, built anew for each pass with what is left
     of the run's limit, so one client serves concurrent calls;
   - the decision gate (D81), the checkpoints, unique call ids (D59), compaction (D32), and the provider's client,
     instrumented (D36), behind a wrapper that never disposes it (D29).
5. The options are a `ChatOptions` subclass that remembers the run options, so the raw path's client gets them too.
   A host's run finds the claim and the entries it took in them.

## Storage

`ISettingsStore<TId>` keeps documents by id and kind, as JSON with a version (D38, D41), `IAgentSettingsStore` keeps
the agents' own by kind alone (D66), `IConversationStore<TId>` keeps conversation entries, and `IDecisionStore<TId>`
keeps one decisions document per conversation, as JSON with a version (D79). `ITypeshiftStore<TId>` is the first and
the last two in one. A host without a settings id reaches `IAgentSettingsStore`, and a run loads from it
only when one is registered. `WithStore<T>()` registers each of them the store implements, of any id type, so a host
may keep settings and conversations in two stores, under ids of two types. A run carries each id as a `StoreId<TId>`,
from `WithSettingsId` and `WithConversationId`, and the id reaches the store of its type through a generic call
(D72). The raw path's `ChatOptions.ConversationId` becomes a `StoreId<string>`. A conversation that
`PersistAllConversations` starts is a `StoreId<Guid>` unless `NewConversationIds` gives another, and on the raw path a
`StoreId<string>`, so its response's id names it again (D73).
`StoreAccess` serializes and reads the documents, so a store handles strings and numbers only. `VerifyAsync` runs the rules a store must keep against it, from the store's own tests (D58).
`WithFileStore` registers the internal `FileAgentSettingsStore`, which keeps agents' own settings in the `settings`
folder of the file store's folder, a file per kind (D67). When the service collection, which `StoreAccess` reads at its
first store call, holds no store, `StoreAccess` keeps everything in an `InMemoryTypeshiftStore` of its own per id type
and warns on the first write (D69). Decisions alone have no store that must exist: without an `IDecisionStore<TId>`
of the id's type a load answers "none" and only a write throws (D79). `DecisionLedger` is the only code that loads
and saves a conversation's decisions document, each change as a compare-and-set on the version it loaded (D80).

The generator writes each agent's `Settings` as an `IStorable<Settings>`, with a kind, a schema version, `Serialize` and
`Deserialize`. It is an `IJsonStorable<Settings>` too, whose `WriteJson` and `TryReadJson` serve `Serialize` and the
`StorableJsonConverter` that lets `System.Text.Json` speak the stored shape. Tool overrides are `IJsonStorable` as
well, so an agent's settings can delegate to the overrides of a toolset in another assembly (D44).

## Conversations

A run with a conversation id goes through `ConversationChatClient`. It claims the conversation in `LiveRuns`, which
allows one run per conversation in a process, then loads the entries from the last summary on
(`LoadConversationSinceCompactionAsync`) and the conversation's decisions, and builds the history without unanswered
calls, errors and the approval contents an earlier version stored. Before the run begins, it settles the batch of
decisions the conversation holds (D80, D81). New input expires an open batch, and the history gets a result for each
of its calls. An answered batch is marked started and handed to the run as a replay. A batch that another run
started and left gets a result for each call that has none. A resume of an open batch returns its decisions without
a run. Then it stores the input, begins the run in `LiveRuns`, and ends the run with an entry of its own. When the
run asked your app, the decisions join the response after that end, in a message of their own that nothing stores.

Inside the tool loop, `CheckpointChatClient` stores the steps before each model call, tool results first and then
any steering input it takes in, and stores each response as it arrives. `ConversationRun` holds what one run stored
and where its steering input went. Streaming runs publish their updates through `LivePublisher` to the watchers
that `ConversationTracker<TId>.WatchAsync` registered (D47). The container makes the tracker of each id type from one
open-generic registration, and `WithConversationTracker<TId>()` closes one for Native AOT (D72).

## Decisions and modes

`ModeChatClient` sits above `FunctionInvokingChatClient`, around each pass of a run. It hands the loop the run's
tools, those that require approval inside a `GatedFunction`, which hides the mark the loop would pause on. In plan
mode it offers only the read-only tools and `exit_plan_mode`, and appends the planning instruction. In every mode it
adds `ask_user` for an agent that declares `UseQuestions()`, in a host's run with a conversation (D82). The run's
tools are complete here, the context providers' included, so this is where `OfferedTools` fails a run whose tools
share a name or take one the run offers itself (D35). It builds the loop of the pass with what is left of the run's
limit.

`DecisionChatClient`, the gate, sits below the loop and above the checkpoints, so it reads each model response
before the loop does. It gives every call that needs one a verdict on `ConversationRun`: the approval rules first,
then the conversation's standing approvals, then the mode. A call of `ask_user` always asks, unless its arguments
break the tool's shape, which the gate answers with what is wrong. When a call is left for your app, it opens a
batch through `DecisionLedger` and marks the response's calls informational, so the loop invokes none and the run
ends. In a run that carries out an answered batch, the gate answers the first model call with the batch's calls in
place of the model. `ToolGate`, the loop's function invoker, returns a rejected call's text and runs every other
call through the tool middleware, and a `GatedFunction` runs only a call the gate cleared (D81).

`AgentHost` has the members that need no run, `GetDecisionsAsync`, `RecordAnswersAsync` and `SetModeAsync`, all
through the ledger. The conversation's mode is in the decisions document, with the standing approvals, so a summary
leaves both alone. A run whose settings name a mode saves it when it takes the conversation, before it learns which
agent answers, and the save that starts an answered batch switches to the mode of an approved plan. When the run
begins, `AgentRun` takes the document's mode, or the agent's own while the document names none (D52).

## Middleware and context

`RunMiddlewareChatClient` sits below the conversation and runs the middleware the run's services resolve, around
each pass of a run. In a streaming run the chain runs alongside the caller's enumeration and hands updates over a
channel. Below it, `ContextChatClient` asks the agent's providers and adds their instructions, messages and tools to
a copy of the options. It marks those messages as stored, so the checkpoints leave them out. Tool middleware is
`FunctionInvokingChatClient`'s function invoker (D30, D31).

## Telemetry and compaction

`ConversationChatClient` opens each run's `invoke_agent` span, on which `ConversationRun` records the decisions the
run asks and what becomes of the calls of a batch it settles. `InstrumentedClients` keeps one Microsoft.Extensions.AI
`OpenTelemetryChatClient` per backend, which passes each call on to the client its run got. The tool loop
writes its tool spans under the run's span, which a streaming run makes current again before it asks for each update
(D36).

`CompactingChatClient` sits below the checkpoints, so every step is stored before it shrinks the call. `Compaction`
holds one run's state: the cleared results, the summary and the message it kept from, the estimate of the call it
sent last and the estimate's calibration (D32, D36).

## Sub-agents and handoff

`UseAgent<T>()` and `UseHandoff<T>()` capture a factory of `AgentHost<T>`, seen as `IAgentHost`. `AgentHost` offers
each sub-agent as a `SubAgentFunction`. It runs the other host with the caller's settings id, mode and standing
approvals, or, in the background, starts it on `ConversationRun`, whose answers join the run through the steering
queue.

`ConversationChatClient` declares the handoff tools when the run has a conversation. When the tool loop passes a
handoff call out, `HandOffAsync` carries it out, for the streaming loop and the other: it answers the calls made
beside the handoff as not run, stops the background sub-agents, counts the run's handoffs against the most a run
makes, swaps the claim, and continues the run with the agent handed to. `RunStart` sends a conversation's next run
to the agent that answered last (D33, D34). Either way the other agent's run takes a `RunHandover`: the settings
id, the conversation, the claim on it, so no other run starts in between (D50), a typed run's format (D64), the
handoffs made so far, and for a run passed on the entries loaded for it. A sub-agent's run takes one with its
caller and no conversation.

## MCP servers

When `AgentHost` resolves a run's options, it asks the host's `IMcpToolProvider` for each server's tools. It offers
them after the agent's own, marked for plan mode or wrapped for approval with the server's name, which a decision
and a standing approval about the tool carry (D35, D81).

## Related

- [Decisions](./decisions.md): why each part is shaped the way it is, by D-number.
- [How a run works](../guide/how-a-run-works.md): the run's steps as a user sees them.
- [AgentHost and generated members](../guide/generated-members.md): what the emitters write, member by member.
