# Generated members

The generator writes these members into your toolsets and agents, and next to them. Each section names the
declaration that triggers it. Members marked hidden are `[EditorBrowsable(Never)]` plumbing that only generated code
calls; their names are taken all the same, and a member of yours with one of them is
[TSH018](./diagnostics.md#tsh018). Names derive from the tool's name in PascalCase: `search_tickets` gives
`SearchTickets`.

## Per toolset

### `{Tool}Overrides`

**Triggered by** a tool with at least one configurable settings member:

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

gives `TicketToolset.SearchTicketsOverrides` with `int? Limit` and `bool? IncludeClosed`, and

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

gives `KnowledgeToolset.SearchArticlesOverrides` with `string? Language`, `IReadOnlyList<string>? Sources` and
`ArticleRanking? Ranking`; `Limit` stays in code.

A `public sealed record` nested in the toolset:

| Member | Is |
|---|---|
| One `init` property per configurable member | The member's type, nullable. Null keeps the layer below. |
| `ApplyTo(TSettings)` (hidden) | The settings of the layer below with every value this layer sets. |
| `WriteJson`, `TryReadJson` (hidden) | Its JSON: camelCase, nulls left out. A `JsonConverter` on the record makes `System.Text.Json` read and write the same shape. |

An agent's `Settings` holds one per offered tool; a public agent cannot name the overrides of a less accessible
toolset ([TSH006](./diagnostics.md#tsh006)).

### `Create{Tool}` and `{Tool}Function`

**Triggered by** every `AddTool(...)`:

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
| `Create{Tool}(settings, json)` (hidden) | A static factory returning the tool's `AIFunction`. It takes the resolved settings only when the tool has settings. |
| `{Tool}Function` | A private nested class deriving from `GeneratedToolFunction`: the name, the description, the schema of the parameters the model fills, and the binding that calls the method. |

### `{Toolset}ToolList`

**Triggered by** every toolset. A static class, as accessible as the toolset, holding an extension block on
`ToolList<TToolset>` with one `ToolName` property per tool, for `Include` and `Exclude`:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`TicketToolsetToolList` gives `ToolList<TicketToolset>` the properties `SearchTickets`, `GetTicket` and `CloseTicket`.
A tool named like a member of `object` gets a `Tool` suffix: `to_string` is `ToStringTool`.

### Stamps

**Triggered by** every tool: a hidden `[GeneratedTool]` attribute on the toolset naming the tool, its list property,
its factory, its settings and overrides types and whether it requires approval. Agents in other assemblies read it to
offer the tool.

## Per agent

### `Settings`

**Triggered by** every agent. A `public sealed record` nested in the agent, holding exactly what may change at
runtime:

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

        UseToolset<TicketToolset>().Include(t => t.SearchTickets).Include(t => t.CloseTicket);

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

gives `SupportAgent.Settings` with `Model`, `Instructions`, `Temperature`, `SearchTickets` and `SearchArticles`.

| Declaration | Member |
|---|---|
| `WithChatOptions().Configurable(o => o.ModelId)` | `string? Model`, over `WithModel`. |
| `WithChatOptions().Configurable(o => o.X)` | `X`, nullable: `float? Temperature`, `string? Instructions`. |
| `WithRunOptions().Configurable(r => r.X)` | `X`, nullable: `AgentMode? Mode`, `int? MaxToolIterations`. |
| `UseToolset<T>()` offering a tool with `{Tool}Overrides` | `{Tool}`, its overrides, never null. A name taken by an option or by a member of the record below gets a `Tool` suffix. |

Every value is nullable, and null keeps the layer below; see [settings layering](./settings-layering.md). The
options that may be configurable are listed in [builders](./builders.md#chatoptionsbuilder).

The record is the document a store keeps under a settings id:

| Member | Is |
|---|---|
| `static string Kind` | The name a store keys it by: `WithStorageKind("...")`, or else the class name, so moving the agent to another namespace keeps its data. Two agents of one kind are [TSH022](./diagnostics.md#tsh022). |
| `static int Version` | 1, or what `WithSettingsVersion(n)` sets. The store keeps it next to the JSON. |
| `Serialize()` | The JSON. |
| `static Deserialize(json, version)` | Reads the JSON; throws a `JsonException` for a version newer than the code's. |
| `WriteJson`, `TryReadJson` (hidden) | What `Serialize`, `Deserialize` and the record's `JsonConverter` use. |

The JSON is camelCase, with the tools nested under `"tools"` by name and nulls left out:

```json
{
  "model": "reasoning-model",
  "instructions": "Answer in German.",
  "tools": {
    "search_tickets": {
      "limit": 20
    }
  }
}
```

A member that does not read, mistyped or unknown, keeps the layer below. A stored record or class is replaced as a
whole, its missing members taking their defaults. A configurable member renamed or removed reads as missing; raise
`WithSettingsVersion` and migrate the older documents in the store when that matters.

### `Configure{Tool}()`

**Triggered by** an offered tool with settings, from `WithSettings<T>()` or `WithConfigurableSettings<T>()`,
configurable members or not:

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

`ConfigureFindCompanies()` is a private method returning a builder with one `With{Member}(value)` per settable member
of the settings record: `WithLimit(int)` and `WithIncludeInactive(bool)`. The values apply over the record's code
defaults, for every run of the agent. A tool the agent does not offer has none, so configuring it does not compile. A
`Configure{Tool}()` the agent declares itself takes the place of the generated one.

### `{Agent}HostExtensions`

**Triggered by** every agent the generated code can reach, not `private` or `protected` nested
([TSH016](./diagnostics.md#tsh016)). A static class, as accessible as the agent, holding an extension block on
`AgentHost<TAgent>` with the agent's typed runs and settings; see [agent host](./agent-host.md).

### Hidden members

Every agent also gets an override of `Declaration`, what the generator read from its constructor, a private nested
class `TypeshiftGenerated` and, when it has a `Configure{Tool}()`, a private field `_typeshift`.

## Per project

| Member | Triggered by | Is |
|---|---|---|
| `{Assembly}Module` (hidden) | A toolset or an agent in the assembly | Registers the assembly's toolsets, agents, hosts and JSON contexts; see [registration](./registration.md#what-addtypeshift-registers). |
| `[GeneratedModule]` (hidden) | The same | Stamps the module, its version and the id type for the projects that reference the assembly. |
| `ITypeshiftStore`, `InMemoryTypeshiftStore`, `ConversationTracker`, `LiveRun` | A reference to the package | `global using` aliases closing the library's types over the [id type](./registration.md#the-id-type). |
| The `AddTypeshift()` interceptor | A call of `services.AddTypeshift()` | See [registration](./registration.md). |
