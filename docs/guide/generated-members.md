---
title: Generated members
description: Every member the generator writes into or next to your toolsets and agents, and what triggers it.
---

# Generated members

The source generator writes these members into your toolsets and agents, and next to them. Each member's section names
the declaration that triggers it. [Toolsets](./toolsets.md) and [Agents](./defining-agents.md) show them in use.

## Names and hidden members

- Member names derive from the tool's name in PascalCase: `search_tickets` gives `SearchTickets`.
- Members marked *hidden* are `[EditorBrowsable(Never)]` plumbing that only generated code calls. Code completion leaves
  them out, but their names are taken all the same: a member of yours with one of them is
  [TSH018](./diagnostics.md#tsh018).

## Per toolset

### `{Tool}Overrides`

**Triggered by** a tool with at least one configurable settings member. The record is a `public sealed record` nested in
the toolset.

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
`ArticleRanking? Ranking`, comes from this one. `Limit` stays in code:

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
`ToolList<TToolset>`. The block has one `ToolName` property per tool, which `Include` and `Exclude` pick from:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TicketToolsetToolList` gives `ToolList<TicketToolset>` the properties `SearchTickets`, `GetTicket` and `CloseTicket`. A
tool named like a member of `object` gets a `Tool` suffix: `to_string` becomes `ToStringTool`.

### Stamps

**Triggered by** every tool. A hidden `[GeneratedTool]` attribute on the toolset names the tool, its list property, its
factory, its settings and overrides types, and whether it requires approval. Agents in other assemblies read it to offer
the tool.

## Per agent

### `Settings`

**Triggered by** every agent. The record is a `public sealed record` nested in the agent, and holds exactly what may
change at runtime. This agent gets `SupportAgent.Settings` with `Model`, `Instructions`, `Temperature`, `SearchTickets`
and `SearchArticles`:

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

Every value is nullable, and null keeps the layer below; see [Settings layering](./settings-and-stores.md#settings-layering). The options
that may be configurable are listed in [Builders](./builders.md#chatoptionsbuilder).

The record is also the document a store keeps under a settings id:

| Member | Is |
|---|---|
| `static string Kind` | The name a store keys it by: `WithStorageKind("...")`, or else the class name, so moving the agent to another namespace keeps its data. Two agents of one kind are [TSH022](./diagnostics.md#tsh022). |
| `static int Version` | 1, or what `WithSettingsVersion(n)` sets. The store keeps it next to the JSON. |
| `Serialize()` | The JSON. |
| `static Deserialize(json, version)` | Reads the JSON. Throws a `JsonException` for a version newer than the code's. |
| `WriteJson`, `TryReadJson` (hidden) | What `Serialize`, `Deserialize` and the record's `JsonConverter` use. |

The JSON's shape, how a store reads it back, and what to do when a configurable member is renamed are in
[Settings and stores](./settings-and-stores.md#agent-settings).

### `Configure{Tool}()`

**Triggered by** an offered tool with settings, from `WithSettings<T>()` or `WithConfigurableSettings<T>()`, whether it
has configurable members or not:

<!-- snippet: quickstart-agent -->
<a id='snippet-quickstart-agent'></a>
```cs
internal partial class ResearchAgent : Agent
{
    public ResearchAgent()
    {
        WithDescription("Researches the companies of a city.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.2f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<CompanyToolset>();

        ConfigureFindCompanies()
            .WithLimit(3);
    }

    protected override string GetInstructions() =>
        """
        Find companies with the find_companies tool and list them.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Quickstart/ResearchAgent.cs#L3-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-quickstart-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- `ConfigureFindCompanies()` is a private method. It returns a builder with one `With{Member}(value)` per settable
  member of the settings record: here `WithLimit(int)` and `WithIncludeInactive(bool)`.
- The values apply over the record's code defaults, for every run of the agent.
- A tool the agent does not offer has no `Configure{Tool}()`, so configuring it does not compile.
- A `Configure{Tool}()` the agent declares itself takes the place of the generated one.

### `{Agent}HostExtensions`

**Triggered by** every agent the generated code can reach: one that is not `private` or `protected` nested
([TSH016](./diagnostics.md#tsh016)). A static class, as accessible as the agent, holds an extension block on
`AgentHost<TAgent>` with the agent's typed runs and settings. [Agent host](./agent-host.md#generated-members) lists
them.

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

## Related

- [Toolsets](./toolsets.md): the declarations behind the per-toolset members.
- [Agents](./defining-agents.md): the declarations behind the per-agent members.
- [Agent host](./agent-host.md): the typed runs and settings members of `{Agent}HostExtensions`.
- [Settings and stores](./settings-and-stores.md): how a store keeps an agent's `Settings`.
- [Builders](./builders.md): every call the generator reads.
