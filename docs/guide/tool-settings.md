# Tool settings

Tool settings are values a tool needs that the model must not choose: a result limit, an endpoint, a feature flag.
They live in a record, the tool method receives them as a parameter the model never sees, and each layer above the
code may change only the members the tool marks configurable.

## The settings record

<!-- snippet: settings-record -->
<a id='snippet-settings-record'></a>
```cs
internal record SearchTicketsSettings
{
    public int Limit { get; init; } = 10;

    public bool IncludeClosed { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
```
<sup><a href='https://github.com/weboost-at/Agentic/blob/main/samples/Agentic.Samples.Docs/Tickets/SearchTicketsSettings.cs#L3-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The record's parameterless constructor gives the code defaults. It must be a record
([AGT005](./diagnostics.md#agt005)), because overrides apply to a `with` copy.

## Configurable members

`Configurable(...)` calls chained after `WithSettings<T>()` mark the members that may change at runtime:

<!-- snippet: settings-picks -->
<a id='snippet-settings-picks'></a>
```cs
AddTool(SearchTickets)
    .WithName("search_tickets")
    .WithSettings<SearchTicketsSettings>()
    .Configurable(s => s.IncludeClosed)
    .Configurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Agentic/blob/main/samples/Agentic.Samples.Docs/Tickets/TicketToolset.cs#L13-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-picks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The generator reads the picks at compile time; at runtime their lambdas never run. Members left out, `Timeout` here,
only ever have their code value. Configurable members must have a type the generated records can store
([AGT014](./diagnostics.md#agt014)): `bool`, the numeric types, `string`, `Guid`, `DateTime`, `DateTimeOffset`,
`TimeSpan`, enums, string lists, and their nullable forms.

## Receiving settings in the tool

The tool method takes a parameter of its settings type and receives the resolved settings there. The parameter is
not part of the model's schema, so the model can never set it; a tool without such a parameter gets a warning
([AGT009](./diagnostics.md#agt009)):

<!-- snippet: read-settings -->
<a id='snippet-read-settings'></a>
```cs
public static IReadOnlyList<Ticket> SearchTickets(
    string query,
    SearchTicketsSettings settings,
    IServiceProvider services) =>
    services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);
```
<sup><a href='https://github.com/weboost-at/Agentic/blob/main/samples/Agentic.Samples.Docs/Tickets/TicketToolset.cs#L32-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-read-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each call runs in a DI scope of its own, so two calls with different settings never see each other's values.

## The generated overrides

For a tool with configurable members, the generator writes a nested `{Tool}Overrides` record in the toolset,
`TicketToolset.SearchTicketsOverrides`, with each configurable member as a nullable property. Overrides are how every
layer above the code speaks about the tool: stored defaults, an agent's stored settings and a run's settings. A null
value keeps what the layer below set.

## The layers

Settings resolve per run from five layers, lowest first:

| Layer | Set with | Changes |
|---|---|---|
| 1. Code defaults | The record's constructor | Everything |
| 2. Stored tool defaults | [`ToolDefaults`](./persistence.md#tool-defaults), under an id | Configurable members |
| 3. The agent's code | [`WithToolSettings()`](./agents.md#tool-settings) | Any member, over the code defaults |
| 4. The agent's stored settings | [`ConfigureAsync`](./persistence.md#agent-settings), under an id | Configurable members |
| 5. The run | [Run settings](./running-agents.md#run-settings) | Configurable members |

Stored tool defaults apply only to agents whose code does not set the tool's settings: an agent that sets them in
code has decided, and only its own stored settings or a run may change the configurable members.
