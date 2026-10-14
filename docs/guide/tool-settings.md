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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/SearchTicketsSettings.cs#L3-L12' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-record' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Its parameterless constructor gives the code defaults. It must be a record ([TSH005](./diagnostics.md#tsh005)),
since each layer of settings applies to a `with` copy.

## Configurable members

Configurable members are an allow list. `WithSettings<T>()` keeps every member in code unless `Configurable(...)`
names it:

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

`WithConfigurableSettings<T>()` lets every settable member change unless `NotConfigurable(...)` names it:

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

After `WithSettings<T>()` only `Configurable` follows, after `WithConfigurableSettings<T>()` only `NotConfigurable`.
The generator reads the picks; their lambdas never run, and a pick must be `s => s.Member`
([TSH015](./diagnostics.md#tsh015)). Obsolete members stay out on their own. A configurable member needs a public
`set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)).

A configurable member's type is one settings can store: scalars, enums, records and classes of storable members,
collections and dictionaries of them. [Builders](./builders.md#configurable-members) lists them
([TSH012](./diagnostics.md#tsh012)).

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L30-L40' title='Snippet source file'>snippet source</a> | <a href='#snippet-read-settings' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Changing settings at runtime

For a tool with configurable members, the generator writes a `{Tool}Overrides` record into the toolset,
`TicketToolset.SearchTicketsOverrides`, with each configurable member nullable; see
[generated members](./generated-members.md#tool-overrides). Each agent that offers the tool holds one in its
`Settings`. Set the values:

- for every run of one agent, in its code: [`Configure{Tool}()`](./defining-agents.md#tool-settings), which may set
  any settable member;
- for every run with a settings id: [stored settings](./settings-and-stores.md#agent-settings);
- for one run: the [run's settings](./running-agents.md#run-settings).

A value left null keeps the one below; [settings layering](./settings-layering.md) shows the order.
