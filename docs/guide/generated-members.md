---
title: AgentHost and generated members
description: Every member the generator writes for your toolsets and agents, what triggers it, and every member of AgentHost<TAgent> with its signature.
---

# AgentHost and generated members

The generator adds members to your toolsets and agents, and to `AgentHost<TAgent>`, the class that runs an agent. Each
section names the declaration that adds a member.

## Names and hidden members

- Member names come from the tool's name in PascalCase, so `search_tickets` gives `SearchTickets`.
- Members marked *hidden* are `[EditorBrowsable(Never)]` plumbing that only generated code calls. Code completion
  leaves them out in a project that references your compiled assembly, such as a consumer of your package. The
  project that declares them and the other projects of its solution still see them, because Roslyn, the code
  completion behind Visual Studio and VS Code, hides such members only when they come from a compiled reference.
- The names of hidden members are taken all the same, so the analyzer reports [TSH018](./diagnostics.md#tsh018) for a
  member of yours with one of them.
- Every generated class, and every member the generator adds to a class of yours, carries `[GeneratedCode]` and
  `[ExcludeFromCodeCoverage]`, so coverage tools that honor them leave the generated code out of your numbers. Your
  own toolset and agent classes are not marked, so analyzers and coverage still see the code you wrote in them.
- The members of the host and the tools' factories carry `[DebuggerStepThrough]`, so a step into
  `host.RunAsync(...)` does not stop in a generated forwarder.

## Per toolset

### `{Tool}Overrides`

**Triggered by** a tool with at least one configurable settings member. The record is a `public sealed record`
nested in the toolset, or in the agent for a tool of the agent's own.

`TicketToolset.SearchTicketsOverrides`, with `int? Limit` and `bool? IncludeClosed`, comes from this declaration:

<!-- snippet: settings-picks -->
<a id='snippet-settings-picks'></a>
```cs
AddTool(SearchTickets)
    .ReadOnly()
    .WithSettings<SearchTicketsSettings>()
    .Configurable(s => s.IncludeClosed)
    .Configurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L12-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-picks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`KnowledgeToolset.SearchArticlesOverrides`, with `string? Language`, `IReadOnlyList<string>? Sources` and
`ArticleRanking? Ranking`, comes from this one, where `Limit` stays in code:

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Member | Is |
|---|---|
| One `init` property per configurable member | The member's type, nullable. Null keeps the layer below. A member named like the record or one of its own members, such as `ApplyTo`, gets a `Value` suffix, so its property is `ApplyToValue`; the JSON keeps the member's own name. |
| `ApplyTo(TSettings)` (hidden) | Returns the settings of the layer below, with every value this layer sets applied over them. |
| `WriteJson`, `TryReadJson` (hidden) | Write and read the record's JSON, in camelCase with nulls left out. A `JsonConverter` on the record makes `System.Text.Json` read and write the same shape. |

An agent's `Settings` holds the overrides of every tool it offers. So a toolset with configurable settings must be as
accessible as a public agent that offers it ([TSH006](./diagnostics.md#tsh006)).

### `Create{Tool}` and `{Tool}Function`

**Triggered by** every `AddTool(...)`. This one gives `CreateCloseTicket` and `CloseTicketFunction`:

<!-- snippet: approval-tool -->
<a id='snippet-approval-tool'></a>
```cs
AddTool(CloseTicketAsync)
    .RequiresApproval();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L24-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Member | Is |
|---|---|
| `Create{Tool}(settings, json)` (hidden) | A static factory that returns the tool's `AIFunction`. `json`, a `ToolJson`, sets how the tool binds its arguments and serializes its result. It takes `settings`, the tool's settings after layering, only when the tool has settings. |
| `{Tool}Function` | A private nested class deriving from `GeneratedToolFunction`. It holds the name, the description, the schema of the parameters the model fills, and the binding that calls the method. |

### The tool list

**Triggered by** every toolset the generated code can reach ([TSH016](./diagnostics.md#tsh016)). A static class, as
accessible as the toolset, holds an extension block on `ToolList<TToolset>`. The block has one property per tool,
named after the tool and of type `ToolName`, and `Include` and `Exclude` pick from these properties:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>()
    .Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The block gives `ToolList<TicketToolset>` the properties `SearchTickets`, `GetTicket` and `CloseTicket`. A tool
named like a member of `object` gets a `Tool` suffix, so `to_string` becomes `ToStringTool`.

The class lives in the global namespace, so the properties are in scope in every file, whatever it imports: a pick
compiles also where the file names the toolset in full, as in `UseToolset<Demo.TicketToolset>()`, or through an
alias. Its name spells out the toolset's full name, each part after its length, such as
`TypeshiftTools_4Demo13TicketToolset` for `Demo.TicketToolset`, so no two toolsets share one and no type of yours
meets it. The class is [hidden](#names-and-hidden-members), since you pick from the properties and never need
its name.

### Stamps

**Triggered by** every tool. A hidden `[GeneratedTool]` attribute on the toolset names the tool, its list property,
its factory, its settings and overrides types, and whether it requires approval. Agents in other assemblies read it
to offer the tool.

## Per agent

### `Settings`

**Triggered by** every agent. The record is a `public sealed record` nested in the agent, and holds exactly what may
change at runtime. This agent gets `SupportAgent.Settings` with `Provider`, `Model`, `Instructions`, `Temperature`,
`SearchTickets` and `SearchArticles`:

<!-- snippet: agent -->
<a id='snippet-agent'></a>
```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>()
            .Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page")
            .ReadOnly();

        UseContext<CustomerProfileContext>();

        UseQuestions();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L52' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Declaration | Member |
|---|---|
| `WithChatOptions().Configurable(o => o.ModelId)` | Two members: `Provider`, of the agent's [provider id type](./models.md#the-provider-id-type), such as `string?`, and `string? Model`. They override the provider and model that `WithModel` sets in code. |
| `WithRunOptions().Configurable(r => r.SummaryModel)` | `SummaryProvider`, of the agent's provider id type, and `string? SummaryModel`. If either is still null after every layer, the summary uses the run's own provider or model. |
| `WithChatOptions().Configurable(o => o.X)` | `X`, nullable: `float? Temperature`, `string? Instructions`. |
| `WithRunOptions().Configurable(r => r.X)` | `X`, nullable: `AgentMode? Mode`, `int? MaxToolIterations`. |
| `UseToolset<T>()` offering a tool with `{Tool}Overrides` | `{Tool}`, its overrides, never null. A name taken by an option or by one of the record's own members, such as `Kind`, gets a `Tool` suffix. |

The option members and the members of each tool's overrides are nullable, and null keeps the layer below
([Settings layering](./settings-and-stores.md#settings-layering)).
[Builders](./builders.md#chatoptionsbuilder) lists the options that may be configurable.

The record is also the document a store keeps under a settings id:

| Member | Is |
|---|---|
| `static string Kind` | The name a store keys it by, which the store's methods receive as `kind`: `WithStorageKind("...")`, or else the class name, so moving the agent to another namespace keeps its data. For two agents of the same kind, the analyzer reports [TSH022](./diagnostics.md#tsh022). |
| `static int SchemaVersion` | 1, or what `WithSchemaVersion(n)` sets, which `Serialize()` writes into the JSON ([The stored JSON](./settings-and-stores.md#the-stored-json)). |
| `Serialize()` | Writes the record as JSON. |
| `static Deserialize(json)` | Reads the JSON. Throws a `JsonException` for a `"$schemaVersion"` newer than the code's. |
| `WriteJson`, `TryReadJson` (hidden) | What `Serialize`, `Deserialize` and the record's `JsonConverter` use. |

[The stored JSON](./settings-and-stores.md#the-stored-json) covers the JSON's shape, how it reads back, and what to
do when a configurable member is renamed.

### `Configure{Tool}()`

**Triggered by** an offered tool with settings, from `WithSettings<T>()` or `WithConfigurableSettings<T>()`, whether
it has configurable members or not. A settings record without a settable member leaves nothing to set, so its tool
gets none.

`ConfigureSearchTickets()` in the `SupportAgent` above is a private method. It returns a builder with one
`With{Member}(value)` per settable member of the settings record, configurable or not. Each one shows the member's
`<summary>` in code completion. `SearchTicketsSettings` has
`Limit`, `IncludeClosed` and a `Timeout` that is not configurable, so the builder has `WithLimit(int)`,
`WithIncludeClosed(bool)` and `WithTimeout(TimeSpan)`. The values apply over the record's code defaults, for every
run of the agent.

A tool the agent does not offer has no `Configure{Tool}()`, so configuring it does not compile. A `Configure{Tool}()`
the agent declares itself replaces the generated one.

### Tools of the agent's own

**Triggered by** an `AddTool(...)` in the agent's constructor. The agent gets the tool's `Create{Tool}` and
`{Tool}Function`, and its `{Tool}Overrides` when it has configurable members, as a toolset does. It gets no tool list
and no toolset stamp.

### Host members

**Triggered by** every agent the generated code can reach: one that is not nested as `private` or `protected` and
has a public or internal parameterless constructor. For any other agent, the analyzer reports
[TSH016](./diagnostics.md#tsh016). A static class, as accessible as the agent, holds an extension block on
`AgentHost<TAgent>` with the agent's [typed members](#typed-members).

The class lives in the global namespace, so the typed members are in scope in every file, whatever it imports. Its
name spells out the agent's full name, each part after its length, such as `TypeshiftHost_4Demo12SupportAgent` for
`Demo.SupportAgent`, so no two agents share one. The class is [hidden](#names-and-hidden-members), since you call the
members on the host and never need its name: type completion leaves it out where your assembly is a compiled
reference, and lists it in the projects of your own solution.

### Hidden members

Every agent also gets:

- an override of `Declaration`, which holds what the generator read from its constructor;
- a private nested class `TypeshiftGenerated`;
- a private field `_typeshift`, when it has a `Configure{Tool}()`.

## Per project

| Member | Triggered by | Is |
|---|---|---|
| `{Assembly}Module` (hidden) | A toolset, an agent, a JSON context or XML docs in the project | The assembly's [registrations](./registration.md#what-addtypeshift-registers) |
| `[GeneratedModule]` (hidden) | The same | The module's stamp |
| `TypeshiftSchemaAnnotations_{Assembly}` (hidden) | XML docs, which are on unless the project turns them off | What collects the [XML docs of your types](./registration.md#xml-docs-of-parameter-types) |
| The interceptor | A call of `services.AddTypeshift()` or `TypeshiftRuntime.Create(...)` | See [Registration](./registration.md) |

- The module registers the assembly's toolsets, agents, hosts, JSON contexts and XML docs. A project without a
  toolset or an agent still gets one for its `TypeshiftSchemaAnnotations_{Assembly}` class, which every project with
  XML docs on has, and for a [JSON context](./native-aot.md#tool-parameter-and-result-types) it holds.
- `[GeneratedModule]` stamps the module and its version for the projects that reference the assembly.
- The package's build adds `TypeshiftSchemaAnnotations_{Assembly}`, not the generator. It does so while
  `GenerateDocumentationFile` is on, which it is unless the project turns it off, and `TypeshiftSchemaAnnotations`
  is not `false`. The class carries the `[SchemaAnnotations]` attribute that makes Weboost.SchemaAnnotations collect
  the XML docs of your types.

## The agent host

`AgentHost<TAgent>` runs one agent. Its own members are `Agent`, `WithSettingsId`, `WithConversationId`,
`CreateChatClientAsync`, the members for [the decisions of a conversation](#decisions-of-the-conversation), `DisposeAsync`
and `Dispose`, and the generator adds the typed runs and settings of each agent.

### The host's scope

`AddTypeshift()` registers a host per agent as transient, so a host runs with the services of the scope it was
resolved from. A host from `TypeshiftRuntime.Agent<TAgent>()` runs in a scope the runtime created for it
([The runtime](./registration.md#the-runtime)). Tools, the chat client provider, middleware, context providers,
approval rules and the MCP tool provider all resolve from the host's scope.

Every member that loads or saves settings under a settings id needs an `ISettingsStore<TId>` of the id's type, and
every member that loads or appends to a conversation needs an `IConversationStore<TId>` of the conversation id's type.
[Which store serves a call](./implementing-a-store.md#which-store-serves-a-call) says what happens without one.

### `Agent`

`TAgent Agent { get; }` is the agent as its code declares it, a singleton. `Agent.Name` reads what `WithName`
declared, and `Agent.Description` what `WithDescription` or the agent's `<summary>` did.

### `WithSettingsId` and `WithConversationId`

`AgentHost<TAgent> WithSettingsId<TId>(TId id)` and `AgentHost<TAgent> WithConversationId<TId>(TId id)`, where `TId`
is any type but a nullable one.

Both return a copy of the host with the id, in the host's scope, and throw an `ArgumentNullException` for a null id.
[Ids of your own types](./settings-and-stores.md#ids-of-your-own-types) explains the copy and the store its id picks:
an `ISettingsStore<TId>` for a settings id, an `IConversationStore<TId>` for a conversation id.

### `CreateChatClientAsync`

`Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)`

`CreateChatClientAsync` creates the client of the [raw path](./running-agents.md#the-raw-path) for options that
`ResolveOptionsAsync` returned. It takes the client for the options' provider and model, as a run does
([Registering clients](./models.md#registering-clients)), and wraps it in the run's pipeline, with the agent's run
options and the tool loop.

Calling the client with those options runs and stores like `RunAsync`, in the conversation of the host that resolved
them. On options of a host without a conversation id, `ChatOptions.ConversationId` can name one, a `string` id in the
`IConversationStore<string>`. Disposing the client does not dispose the one your provider returned.

It throws an `InvalidOperationException` when no client is registered for the options' provider and model, and none
for every agent.

### Decisions of the conversation

These members work on the decisions of the host's conversation, the one `WithConversationId(id)` gave it. None
starts a run, each takes a `CancellationToken ct = default` last, and each throws an `InvalidOperationException` on
a host without a conversation id.

| Member | Returns |
|---|---|
| `GetDecisionsAsync()` | `Task<ConversationDecisions>` |
| `RecordAnswersAsync(IReadOnlyList<DecisionAnswer> answers)` | `Task<ConversationDecisions>` |
| `SetModeAsync(AgentMode mode)` | `Task` |
| `RevokeStandingApprovalAsync(string tool, string? server = null)` | `Task` |

`GetDecisionsAsync` reads what the conversation waits for in one load, without its history. `Open` lists the
decisions your app has not answered, `CanResume` says whether the answered ones wait to be carried out, `Standing`
lists the tools approved for the conversation, and `Mode` is the conversation's mode, null while nothing named one
([When a call needs approval](./tool-approval.md#when-a-call-needs-approval)).

`RecordAnswersAsync` records answers and continues no run. It throws what
[`AnswerAsync`](#answerasync-and-answerstreamingasync) throws
([Approving or rejecting](./tool-approval.md#approving-or-rejecting)).

`SetModeAsync` switches the conversation's mode from its next run on
([Setting the mode](./tool-approval.md#setting-the-mode)).

`RevokeStandingApprovalAsync` withdraws a standing approval, with its `server` for an MCP server's tool, and
changes nothing when the conversation holds none for the tool
([Approving for the conversation](./tool-approval.md#approving-for-the-conversation)).

The three that save throw a `StoreConflictException` when ten saves in a row lost to another writer.
`RecordAnswersAsync` and `SetModeAsync` throw an `InvalidOperationException` when your app registered stores and
none of them [keeps decisions](./implementing-a-store.md#which-store-serves-a-call).

### `DisposeAsync` and `Dispose`

`ValueTask DisposeAsync()` and `void Dispose()` dispose the scope of a host that `TypeshiftRuntime.Agent<TAgent>()`
created, and with it the scoped services its runs resolved. A host your container resolved, such as one injected into
a request, owns nothing, so disposing it does nothing. Dispose a host once its runs and streams are done, since they
use its scope. `Dispose` throws an `InvalidOperationException` when a scoped service implements only
`IAsyncDisposable`, so prefer `await using`.

### Typed members

In these signatures, `Settings` is the agent's [generated `Settings` record](#settings). Every member takes a
`CancellationToken ct = default` last. None takes an id: each uses the host's, from
[`WithSettingsId` and `WithConversationId`](#withsettingsid-and-withconversationid).

| Member | Returns |
|---|---|
| `RunAsync(string input, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync(ChatMessage input, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync<TResult>(string input, Settings? settings = null)` | `Task<ChatResponse<TResult>>` |
| `RunAsync<TResult>(ChatMessage input, Settings? settings = null)` | `Task<ChatResponse<TResult>>` |
| `RunStreamingAsync(string input, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `RunStreamingAsync(ChatMessage input, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `RunStreamingAsync<TResult>(string input, Settings? settings = null)` | `StreamingRun<TResult>` |
| `RunStreamingAsync<TResult>(ChatMessage input, Settings? settings = null)` | `StreamingRun<TResult>` |
| `ResumeAsync(Settings? settings = null)` | `Task<ChatResponse>` |
| `ResumeStreamingAsync(Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `AnswerAsync(IReadOnlyList<DecisionAnswer> answers, Settings? settings = null)` | `Task<ChatResponse>` |
| `AnswerAsync<TResult>(IReadOnlyList<DecisionAnswer> answers, Settings? settings = null)` | `Task<ChatResponse<TResult>>` |
| `AnswerStreamingAsync(IReadOnlyList<DecisionAnswer> answers, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `AnswerStreamingAsync<TResult>(IReadOnlyList<DecisionAnswer> answers, Settings? settings = null)` | `StreamingRun<TResult>` |
| `ResolveOptionsAsync(Settings? settings = null)` | `Task<ChatOptions>` |
| `LoadSettingsAsync()` | `Task<Stored<Settings>>` |
| `SaveSettingsAsync(Settings settings, long loadedVersion)` | `Task<long>` |
| `ConfigureAsync(Func<Settings, Settings> configure)` | `Task<Settings>` |

### `RunAsync`

`RunAsync` runs the agent on one message, a `string` or a `ChatMessage`, which can carry images.
It layers the agent's code, the stored settings, the conversation's mode, then `settings`
([Settings layering](./settings-and-stores.md#settings-layering)).

On a host with a conversation id, it stores the run as it goes ([What a run stores](#what-a-run-stores)). Without
one, it stores nothing, offers no handoffs and no `ask_user`, and cannot ask for approval, unless your app keeps
every conversation
([Keeping every conversation](./conversations.md#keeping-every-conversation)). A run that cannot ask throws a
`DecisionUnavailableException`, an `InvalidOperationException`, when the model calls a tool that requires approval
and no rule or mode approves the call. To avoid that, run with a conversation id, approve the call with a rule, or
run in `Auto` mode.

When a run ends asking your app, the response's `Decisions` lists what it asks
([When a call needs approval](./tool-approval.md#when-a-call-needs-approval)). A message that carries
Microsoft.Extensions.AI's approval contents throws an `ArgumentException`: answers go through `AnswerAsync`.

`RunAsync` returns the model's messages with the tool calls and results between them, and each message carries the
answering agent's name as `AuthorName`.

`RunAsync<TResult>` asks the model for JSON of `TResult`'s schema and returns it typed in `Result`. Under
[Native AOT](./native-aot.md), `TResult` needs JSON metadata.

### `RunStreamingAsync`

`RunStreamingAsync` layers settings and stores the run like `RunAsync`, but streams the updates, with the tool calls
running between them. The updates of a run with a conversation are also published to
`ConversationTracker<TId>.WatchAsync`. `RunStreamingAsync<TResult>` is its typed form
([Structured output](./running-agents.md#structured-output)).

### `ResumeAsync` and `ResumeStreamingAsync`

Both continue the host's conversation, the one `WithConversationId(id)` gave it, without new input: the interrupted
run from its last stored step, or the decisions your app answered. They layer like `RunAsync` and store the
continuation in the same conversation. A tool call that was still running when the run stopped is left out, so the
model decides again and may call the tool a second time ([Resuming](./conversations.md#resuming)). While decisions
are still open, they call no model and return them. They throw an `InvalidOperationException` when the host has no
conversation id or the conversation has nothing to continue.

### `AnswerAsync` and `AnswerStreamingAsync`

Both record your app's answers to decisions of the host's conversation and, once no decision is left open, carry
them out and continue the run, as `ResumeAsync` does
([Approving or rejecting](./tool-approval.md#approving-or-rejecting)). With decisions left open they call no model,
and what they return lists the rest. `AnswerStreamingAsync` records when its updates are read. The typed forms ask
the continued run for `TResult`.

They throw a `DecisionClosedException` when an answer names a decision that is not open, an `ArgumentException` when
an answer does not fit its decision, one decision is answered twice or the list is empty, and an
`InvalidOperationException` when the host has no conversation id. Nothing is recorded then.

### `ResolveOptionsAsync`

`ResolveOptionsAsync` layers the agent's code, the stored settings, then `settings`
([Settings layering](./settings-and-stores.md#settings-layering)), and returns the run's `ChatOptions` without
storing anything. The options hold the model, the instructions, the configurable
options and the tools. The tools come in this order: the toolsets' tools with their layered settings, the agent's own
tools, the sub-agents, the tools of `WithTools`, then the MCP servers' tools. A run with a conversation adds the
handoff tools last.

### `LoadSettingsAsync`

`LoadSettingsAsync` loads the agent's own settings, through the `IAgentSettingsStore`, or on a host from
`WithSettingsId(id)` the settings stored under that id. It returns them with their version, or empty settings at
version 0 when none are stored. It layers nothing, so the record holds only what was stored. `SaveSettingsAsync` and
`ConfigureAsync` save the same settings.

### `SaveSettingsAsync`

`SaveSettingsAsync` saves the settings `LoadSettingsAsync` loads, if the stored version is still `loadedVersion`:
the version `LoadSettingsAsync` returned, or 0 when it loaded none. It returns the new version, which your app passes
to its next save. It throws a `StoreConflictException` when another save came first.

### `ConfigureAsync`

`ConfigureAsync` loads the settings, applies `configure`, and saves them with the loaded version. On a conflict it
loads and applies again until its save wins, then returns what it saved. After ten conflicts in a row it throws the
last `StoreConflictException`.

### What a run stores

A run with a conversation id stores its input, each model response, each tool result and an entry that ends the run.
[Conversations](./conversations.md#what-is-stored) lists what is stored when a run fails, is cancelled or is cut off,
and [compaction](./conversations.md#compaction) what a compacted run stores.

## Related

- [Toolsets](./toolsets.md): the declarations behind the per-toolset members.
- [Agents](./defining-agents.md): the declarations behind the per-agent members.
- [Using an agent](./using-an-agent.md): getting a host from a request's or a job's scope.
- [Running an agent](./running-agents.md): runs with settings, typed output or streaming.
- [Conversations](./conversations.md): storing, resuming and steering a conversation.
- [Settings and stores](./settings-and-stores.md#agent-settings): loading, saving and configuring an agent's
  settings.
- [Builders](./builders.md): every call the generator reads.
- [How Typeshift reads your code](./how-typeshift-reads-your-code.md): when the generator reads your code, and why.
