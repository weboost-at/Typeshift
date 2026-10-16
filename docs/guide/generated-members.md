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
  leaves them out, but their names are taken all the same, and a member of yours with one of them is
  [TSH018](./diagnostics.md#tsh018).

## Per toolset

### `{Tool}Overrides`

**Triggered by** a tool with at least one configurable settings member. The record is a `public sealed record`
nested in the toolset.

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
| `ApplyTo(TSettings)` (hidden) | The settings of the layer below, with every value this layer sets. |
| `WriteJson`, `TryReadJson` (hidden) | Its JSON: camelCase, nulls left out. A `JsonConverter` on the record makes `System.Text.Json` read and write the same shape. |

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
| `Create{Tool}(settings, json)` (hidden) | A static factory that returns the tool's `AIFunction`. It takes the resolved settings only when the tool has settings. |
| `{Tool}Function` | A private nested class deriving from `GeneratedToolFunction`. It holds the name, the description, the schema of the parameters the model fills, and the binding that calls the method. |

### `{Toolset}ToolList`

**Triggered by** every toolset. A static class, as accessible as the toolset, holds an extension block on
`ToolList<TToolset>` with one `ToolName` property per tool, which `Include` and `Exclude` pick from:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
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
change at runtime. This agent gets `SupportAgent.Settings` with `Model`, `Instructions`, `Temperature`,
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
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page").ReadOnly();

        UseContext<CustomerProfileContext>();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L44' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Declaration | Member |
|---|---|
| `WithChatOptions().Configurable(o => o.ModelId)` | `string? Model`, over `WithModel`. |
| `WithChatOptions().Configurable(o => o.X)` | `X`, nullable: `float? Temperature`, `string? Instructions`. |
| `WithRunOptions().Configurable(r => r.X)` | `X`, nullable: `AgentMode? Mode`, `int? MaxToolIterations`. |
| `UseToolset<T>()` offering a tool with `{Tool}Overrides` | `{Tool}`, its overrides, never null. A name taken by an option or by one of the record's own members, such as `Kind`, gets a `Tool` suffix. |

Every value is nullable, and null keeps the layer below
([Settings layering](./settings-and-stores.md#settings-layering)).
[Builders](./builders.md#chatoptionsbuilder) lists the options that may be configurable.

The record is also the document a store keeps under a settings id:

| Member | Is |
|---|---|
| `static string Kind` | The name a store keys it by: `WithStorageKind("...")`, or else the class name, so moving the agent to another namespace keeps its data. Two agents of one kind are [TSH022](./diagnostics.md#tsh022). |
| `static int Version` | 1, or what `WithSettingsVersion(n)` sets. The store keeps it next to the JSON. |
| `Serialize()` | The JSON. |
| `static Deserialize(json, version)` | Reads the JSON. Throws a `JsonException` for a version newer than the code's. |
| `WriteJson`, `TryReadJson` (hidden) | What `Serialize`, `Deserialize` and the record's `JsonConverter` use. |

[Settings and stores](./settings-and-stores.md#agent-settings) covers the JSON's shape, how a store reads it back,
and what to do when a configurable member is renamed.

### `Configure{Tool}()`

**Triggered by** an offered tool with settings, from `WithSettings<T>()` or `WithConfigurableSettings<T>()`, whether
it has configurable members or not.

`ConfigureSearchTickets()` in the `SupportAgent` above is a private method. It returns a builder with one
`With{Member}(value)` per settable member of the settings record, configurable or not: `WithLimit(int)`,
`WithIncludeClosed(bool)` and `WithTimeout(TimeSpan)`. The values apply over the record's code defaults, for every
run of the agent.

A tool the agent does not offer has no `Configure{Tool}()`, so configuring it does not compile. A `Configure{Tool}()`
the agent declares itself replaces the generated one.

### `{Agent}HostExtensions`

**Triggered by** every agent the generated code can reach, which is any agent that is not `private` or `protected`
nested ([TSH016](./diagnostics.md#tsh016)). A static class, as accessible as the agent, holds an extension block on
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
| `TypeshiftSchemaAnnotations_{Assembly}` (hidden) | `GenerateDocumentationFile` | Added by the package's build: the class whose XML docs Weboost.SchemaAnnotations collects; see [Registration](./registration.md#xml-docs-of-parameter-types). |
| `ITypeshiftStore`, `InMemoryTypeshiftStore`, `ConversationTracker`, `LiveRun` | A reference to the package | `global using` aliases that close the library's types over the [id type](./registration.md#the-id-type). |
| The `AddTypeshift()` interceptor | A call of `services.AddTypeshift()` | See [Registration](./registration.md). |

## The agent host

`AgentHost<TAgent>` runs one agent. Its own members are `Agent` and `CreateChatClientAsync`, and the generator adds
the typed runs and settings of each agent.

### The host's scope

`AddTypeshift()` registers a host per agent as transient, so a host runs with the services of the scope it was
resolved from. Tools, the chat client provider, middleware, context providers, approval rules and the MCP tool
provider all resolve from that scope.

Every member that loads or saves needs a store. Without one, it throws an `InvalidOperationException` that names
`WithStore<T>()`.

### `Agent`

`TAgent Agent { get; }` is the agent as its code declares it, a singleton. `Agent.Name` and `Agent.Description` read
what `WithName` and `WithDescription` declared.

### `CreateChatClientAsync`

`Task<IChatClient> CreateChatClientAsync(ChatOptions options, CancellationToken ct = default)`

Creates the client of the [raw path](./running-agents.md#the-raw-path) for options that `ResolveOptionsAsync`
returned. It asks the `IChatClientProvider` for the options' model and wraps that client in the run's pipeline, with
the agent's run options and the tool loop.

Calling the client with those options runs and stores like `RunAsync`. `ChatOptions.ConversationId` names the
conversation, as a string of the id type. Disposing the client leaves the provider's client alone.

It throws an `InvalidOperationException` when the options name no model, or when no provider is registered.

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

Runs the agent on one message, a `string` or a `ChatMessage`, which can carry images or approval answers. It layers
the agent's code, the settings stored under `settingsId`, the mode the conversation's last run ended in, then
`settings` ([Settings layering](./settings-and-stores.md#settings-layering)).

With a `conversationId`, it stores the run as it goes ([What a run stores](#what-a-run-stores)). Without one, it
stores nothing, offers no handoffs, and throws when a tool asks for approval that no rule or mode grants. It returns
the model's messages with the tool calls and results between them, and each message carries the answering agent's
name as `AuthorName`.

`RunAsync<TResult>` asks the model for JSON of `TResult`'s schema and returns it typed in `Result`. Under
[Native AOT](./production.md#native-aot), `TResult` needs JSON metadata.

### `RunStreamingAsync`

Layers and stores like `RunAsync`, but streams the updates, with the tool calls running between them. With
`WithRealtimeTracking()`, the updates are also published to `ConversationTracker.WatchAsync`.

### `ResumeAsync` and `ResumeStreamingAsync`

Continue the conversation's interrupted run from its last stored step, without new input. They layer like `RunAsync`
and store the continuation in the same conversation. A call that was in flight when the run stopped is left out, so
the model decides again. They throw an `InvalidOperationException` when the conversation has no interrupted run.

### `ResolveOptionsAsync`

Layers the agent's code, the settings stored under `settingsId`, then `settings`, and returns the run's `ChatOptions`
without storing anything. They hold the model, the instructions, the configurable options and the tools, in this
order: the toolsets' tools with their layered settings, the sub-agents, the tools of `WithTools`, then the MCP
servers' tools.

### `LoadSettingsAsync`

Loads the `Settings` stored under an id, with their revision, or empty settings at revision 0 when none are stored.
It layers nothing, so the record holds only what was stored.

### `SaveSettingsAsync`

Saves `Settings` under an id if the stored revision is still `revision`, and returns the new revision. It throws a
`StoreConflictException` when another save came first.

### `ConfigureAsync`

Loads the settings, applies `configure`, and saves them with the loaded revision. On a conflict it loads and applies
again until its save wins, then returns what it saved.

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
