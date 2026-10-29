---
title: Generated members and the host
description: Every member the generator writes for your toolsets and agents, what triggers it, and every member of AgentHost<TAgent> with its signature.
---

# Generated members and the host

The generator adds members to your toolsets and agents, and to `AgentHost<TAgent>`, the class that runs an agent. Each
section names the declaration that adds a member.

## Names and hidden members

- Member names come from the tool's name in PascalCase, so `search_tickets` gives `SearchTickets`.
- Members marked *hidden* are `[EditorBrowsable(Never)]` plumbing that only generated code calls. Code completion
  leaves them out, but their names are taken all the same, so the analyzer reports
  [TSH018](./diagnostics.md#tsh018) for a member of yours with one of them.

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
| One `init` property per configurable member | The member's type, nullable. Null keeps the layer below. |
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

### `{Toolset}ToolList`

**Triggered by** every toolset. A static class, as accessible as the toolset, holds an extension block on
`ToolList<TToolset>`. The block has one property per tool, named after the tool and of type `ToolName`, and `Include`
and `Exclude` pick from these properties:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>()
    .Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L13-L16' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TicketToolsetToolList` gives `ToolList<TicketToolset>` the properties `SearchTickets`, `GetTicket` and
`CloseTicket`. A tool named like a member of `object` gets a `Tool` suffix, so `to_string` becomes `ToStringTool`.

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
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L48' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Declaration | Member |
|---|---|
| `WithChatOptions().Configurable(o => o.ModelId)` | Two members: `Provider`, of the [provider id type](./models.md#the-provider-id-type), and `string? Model`. They override the provider and model that `WithModel` sets in code. |
| `WithRunOptions().Configurable(r => r.SummaryModel)` | `SummaryProvider`, of the provider id type, and `string? SummaryModel`. If either is still null after every layer, the summary uses the run's own provider or model. |
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
| `static int Version` | 1, or what `WithSettingsVersion(n)` sets. `SaveAsync` receives it, and the store keeps it next to the JSON. |
| `Serialize()` | Writes the record as JSON. |
| `static Deserialize(json, version)` | Reads the JSON. Throws a `JsonException` for a version newer than the code's. |
| `WriteJson`, `TryReadJson` (hidden) | What `Serialize`, `Deserialize` and the record's `JsonConverter` use. |

[The stored JSON](./settings-and-stores.md#the-stored-json) covers the JSON's shape, how it reads back, and what to
do when a configurable member is renamed.

### `Configure{Tool}()`

**Triggered by** an offered tool with settings, from `WithSettings<T>()` or `WithConfigurableSettings<T>()`, whether
it has configurable members or not.

`ConfigureSearchTickets()` in the `SupportAgent` above is a private method. It returns a builder with one
`With{Member}(value)` per settable member of the settings record, configurable or not. `SearchTicketsSettings` has
`Limit`, `IncludeClosed` and a `Timeout` that is not configurable, so the builder has `WithLimit(int)`,
`WithIncludeClosed(bool)` and `WithTimeout(TimeSpan)`. The values apply over the record's code defaults, for every
run of the agent.

A tool the agent does not offer has no `Configure{Tool}()`, so configuring it does not compile. A `Configure{Tool}()`
the agent declares itself replaces the generated one.

### Tools of the agent's own

**Triggered by** an `AddTool(...)` in the agent's constructor. The agent gets the tool's `Create{Tool}` and
`{Tool}Function`, and its `{Tool}Overrides` when it has configurable members, as a toolset does. It gets no tool list
and no toolset stamp.

### `{Agent}HostExtensions`

**Triggered by** every agent the generated code can reach: one that is not nested as `private` or `protected` and
has a public or internal parameterless constructor. For any other agent, the analyzer reports
[TSH016](./diagnostics.md#tsh016). A static class, as accessible as the agent, holds an extension block on
`AgentHost<TAgent>` with the agent's [typed members](#typed-members).

### Hidden members

Every agent also gets:

- an override of `Declaration`, which holds what the generator read from its constructor;
- a private nested class `TypeshiftGenerated`;
- a private field `_typeshift`, when it has a `Configure{Tool}()`.

## Per project

| Member | Triggered by | Is |
|---|---|---|
| `{Assembly}Module` (hidden) | A toolset or an agent in the assembly | Registers the assembly's toolsets, agents, hosts, JSON contexts and XML docs; see [Registration](./registration.md#what-addtypeshift-registers). |
| `[GeneratedModule]` (hidden) | The same | Stamps the module, its version and the id type for the projects that reference the assembly. |
| `TypeshiftSchemaAnnotations_{Assembly}` (hidden) | `GenerateDocumentationFile`, unless `TypeshiftSchemaAnnotations` is `false` | Added by the package's build, not the generator. It carries the `[SchemaAnnotations]` attribute that makes Weboost.SchemaAnnotations collect the XML docs of your types; see [Registration](./registration.md#xml-docs-of-parameter-types). |
| `ITypeshiftStore`, `ISettingsStore`, `IConversationStore`, `InMemoryTypeshiftStore`, `ConversationTracker`, `LiveRun`, `IChatClientProvider` | A reference to the package | `global using` aliases that close the library's types over the [id type](./registration.md#the-id-type), and `IChatClientProvider` over the [provider id type](./models.md#the-provider-id-type). |
| The `AddTypeshift()` interceptor | A call of `services.AddTypeshift()` | See [Registration](./registration.md). |

## The agent host

`AgentHost<TAgent>` runs one agent. Its own members are `Agent` and `CreateChatClientAsync`, and the generator adds
the typed runs and settings of each agent.

### The host's scope

`AddTypeshift()` registers a host per agent as transient, so a host runs with the services of the scope it was
resolved from. Tools, the chat client provider, middleware, context providers, approval rules and the MCP tool
provider all resolve from that scope.

Every member that loads or saves settings needs an `ISettingsStore`, and every member that loads or appends to a
conversation needs an `IConversationStore`. [Registering a store](./settings-and-stores.md#registering-a-store) says
what happens without one.

### `Agent`

`TAgent Agent { get; }` is the agent as its code declares it, a singleton. `Agent.Name` and `Agent.Description` read
what `WithName` and `WithDescription` declared.

### `CreateChatClientAsync`

`Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)`

`CreateChatClientAsync` creates the client of the [raw path](./running-agents.md#the-raw-path) for options that
`ResolveOptionsAsync` returned. It asks the `IChatClientProvider` for the options' provider and model and wraps that
client in the run's pipeline, with the agent's run options and the tool loop.

Calling the client with those options runs and stores like `RunAsync`. `ChatOptions.ConversationId` names the
conversation, as a string of the id type. Disposing the client does not dispose the one your provider returned.

It throws an `InvalidOperationException` when the options name no provider or no model, or when no
`IChatClientProvider` is registered.

### Typed members

In these signatures, `TId` is the build's [id type](./registration.md#the-id-type) and `Settings` the agent's
[generated `Settings` record](#settings). Every member takes a `CancellationToken ct = default` last.

| Member | Returns |
|---|---|
| `RunAsync(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync(ChatMessage input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `RunAsync<TResult>(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `Task<ChatResponse<TResult>>` |
| `RunStreamingAsync(string input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `RunStreamingAsync(ChatMessage input, TId? settingsId = null, TId? conversationId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `ResumeAsync(TId conversationId, TId? settingsId = null, Settings? settings = null)` | `Task<ChatResponse>` |
| `ResumeStreamingAsync(TId conversationId, TId? settingsId = null, Settings? settings = null)` | `IAsyncEnumerable<ChatResponseUpdate>` |
| `ResolveOptionsAsync(TId? settingsId = null, Settings? settings = null)` | `Task<ChatOptions>` |
| `LoadSettingsAsync(TId settingsId)` | `Task<Stored<Settings>>` |
| `SaveSettingsAsync(TId settingsId, Settings settings, long revision)` | `Task<long>` |
| `ConfigureAsync(TId settingsId, Func<Settings, Settings> configure)` | `Task<Settings>` |

### `RunAsync`

`RunAsync` runs the agent on one message, a `string` or a `ChatMessage`, which can carry images or approval answers.
It layers the agent's code, the settings stored under `settingsId`, the mode the conversation's last run ended in,
then `settings` ([Settings layering](./settings-and-stores.md#settings-layering)).

With a `conversationId`, it stores the run as it goes ([What a run stores](#what-a-run-stores)). Without one, it
stores nothing and offers no handoffs. If the model then calls a tool that requires approval and no rule or mode
approves the call, it throws an `InvalidOperationException`. To avoid that, run with a conversation id, approve the
call with a rule, or run in `Auto` mode.

`RunAsync` returns the model's messages with the tool calls and results between them, and each message carries the
answering agent's name as `AuthorName`.

`RunAsync<TResult>` asks the model for JSON of `TResult`'s schema and returns it typed in `Result`. Under
[Native AOT](./production.md#native-aot), `TResult` needs JSON metadata.

### `RunStreamingAsync`

`RunStreamingAsync` layers settings and stores the run like `RunAsync`, but streams the updates, with the tool calls
running between them. With `WithRealtimeTracking()`, the updates are also published to
`ConversationTracker.WatchAsync`.

### `ResumeAsync` and `ResumeStreamingAsync`

Both continue the conversation's interrupted run from its last stored step, without new input. They layer like
`RunAsync` and store the continuation in the same conversation. A tool call that was still running when the run
stopped is left out, so the model decides again and may call the tool a second time
([Resuming](./conversations.md#resuming)). They throw an `InvalidOperationException` when the conversation has no
interrupted run.

### `ResolveOptionsAsync`

`ResolveOptionsAsync` layers the agent's code, the settings stored under `settingsId`, then `settings`, and returns
the run's `ChatOptions` without storing anything. The options hold the model, the instructions, the configurable
options and the tools. The tools come in this order: the toolsets' tools with their layered settings, the
sub-agents, the tools of `WithTools`, then the MCP servers' tools.

### `LoadSettingsAsync`

`LoadSettingsAsync` loads the `Settings` stored under an id, with their revision, or empty settings at revision 0
when none are stored. It layers nothing, so the record holds only what was stored.

### `SaveSettingsAsync`

`SaveSettingsAsync` saves `Settings` under an id if the stored revision is still `revision`, and returns the new
revision. It throws a `StoreConflictException` when another save came first.

### `ConfigureAsync`

`ConfigureAsync` loads the settings, applies `configure`, and saves them with the loaded revision. On a conflict it
loads and applies again until its save wins, then returns what it saved. After ten conflicts in a row it throws the
last `StoreConflictException`.

### What a run stores

A run with a conversation id stores its input, each model response, each tool result and an entry that ends the run.
[Conversations](./conversations.md#what-is-stored) lists what is stored when a run fails, is cancelled or is cut off,
and [compaction](./conversations.md#compaction) what a compacted run stores.

## Related

- [Toolsets](./toolsets.md): the declarations behind the per-toolset members.
- [Agents](./defining-agents.md): the declarations behind the per-agent members.
- [Running agents](./running-agents.md): resolving a host and running an agent.
- [Conversations](./conversations.md): storing, resuming and steering a conversation.
- [Settings and stores](./settings-and-stores.md#agent-settings): loading, saving and configuring an agent's
  settings.
- [Builders](./builders.md): every call the generator reads.
