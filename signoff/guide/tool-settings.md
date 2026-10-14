# Tool settings

Tool settings are values a tool needs that the model must not choose: a result limit, a language, a timeout. They
live in a record, the tool receives them as a parameter the model never sees, and only the members the tool marks
configurable may change at runtime.

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Tickets/SearchTicketsSettings.cs#L3-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Its parameterless constructor gives the code defaults. It must be a record ([TSH005](./diagnostics.md#tsh005)),
since each layer applies to a `with` copy.

## Configurable members

Configurable members are an allow list. `WithSettings<T>()` keeps every member in code unless `Configurable(...)`
names it:

<!-- snippet: settings-picks -->
<a id='snippet-settings-picks'></a>
```cs
AddTool(SearchTickets)
    .WithSettings<SearchTicketsSettings>()
    .Configurable(s => s.IncludeClosed)
    .Configurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Tickets/TicketToolset.cs#L12-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-picks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`WithConfigurableSettings<T>()` lets every settable member change unless `NotConfigurable(...)` names it:

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

After `WithSettings<T>()` only `Configurable` follows, after `WithConfigurableSettings<T>()` only `NotConfigurable`.
The generator reads the picks; their lambdas never run. A member that must stay in code under
`WithConfigurableSettings<T>()` but cannot be stored is reported until `NotConfigurable` keeps it out.

Configurable members need a setter ([TSH013](./diagnostics.md#tsh013)) and a type the generated records can store
([TSH012](./diagnostics.md#tsh012)):

- `bool`, the numeric types, `string`, `Guid`, `Uri`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`,
  `TimeSpan`;
- enums, stored by name;
- records and classes whose settable members are storable, replaced as a whole;
- arrays, lists and sets of storable values, and dictionaries keyed by `string` or an enum;
- the nullable forms of all of these.

<!-- snippet: settings-nested -->
<a id='snippet-settings-nested'></a>
```cs
internal record SearchArticlesSettings
{
    public string Language { get; init; } = "en";

    public int Limit { get; init; } = 5;

    public IReadOnlyList<string> Sources { get; init; } = ["help-center"];

    public ArticleRanking Ranking { get; init; } = new();
}

internal record ArticleRanking
{
    public double Recency { get; init; } = 0.3;

    public double Popularity { get; init; } = 0.2;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Knowledge/SearchArticlesSettings.cs#L3-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-nested' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Receiving settings in the tool

The tool takes a parameter of its settings type. A tool with settings but no such parameter is
[TSH010](./diagnostics.md#tsh010):

<!-- snippet: read-settings -->
<a id='snippet-read-settings'></a>
```cs
/// <summary>
/// Searches the customer's support tickets, newest first.
/// </summary>
/// <param name="query">Words the ticket's title contains.</param>
public static IReadOnlyList<Ticket> SearchTickets(
    string query,
    SearchTicketsSettings settings,
    IServiceProvider services) =>
    services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Tickets/TicketToolset.cs#L28-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-read-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## The generated overrides

For a tool with configurable members, the generator writes a `{Tool}Overrides` record into the toolset,
`TicketToolset.SearchTicketsOverrides`, with each configurable member nullable. Every layer above the agent's code
speaks through it, and null keeps the layer below.

## The layers

A run resolves settings from four layers, lowest first:

| Layer | Set with | Changes |
|---|---|---|
| 1. The record's code defaults | Its constructor | Everything |
| 2. The agent's code | [`Configure{Tool}()`](./defining-agents.md#tool-settings) | Any member |
| 3. The agent's stored settings | [`ConfigureAsync`](./settings-and-stores.md#agent-settings), under a settings id | Configurable members |
| 4. The run | [Run settings](./running-agents.md#run-settings) | Configurable members |

Chat options layer the same way, from the agent's `WithChatOptions()` up.
