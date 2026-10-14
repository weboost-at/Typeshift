---
title: Tool settings
description: Give a tool values the model must not choose, such as a result limit, and decide which of them may change at runtime.
---

# Tool settings

Tool settings are values a tool needs that the model must not choose, such as a result limit, a language or a timeout.
They live in a record, and the tool receives them as a parameter the model never sees. Only the members the tool marks
configurable may change at runtime, through stored settings or a run.

::: tip The short version

- A settings record is a `record` whose parameterless constructor sets the code defaults.
- `WithSettings<T>()` keeps every member in code unless `Configurable(...)` names it. `WithConfigurableSettings<T>()`
  opens every settable member unless `NotConfigurable(...)` names it.
- A configurable member needs a public `set` or `init` accessor and a type settings can store.
- The tool takes a parameter of the record's type, and the call fills it with the resolved settings.

:::

## The settings record

A settings record holds the values one tool needs. Its parameterless constructor gives the code defaults:

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

It must be a record ([TSH005](./diagnostics.md#tsh005)), since each layer of settings applies to a `with` copy.

## Configurable members

Configurable members are an allow list. `WithSettings<T>()` keeps every member in code unless `Configurable(...)` names
it:

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

`WithConfigurableSettings<T>()` turns it around. Every settable member may change unless `NotConfigurable(...)` names
it:

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The choice decides what happens to a member you add to the record later. With `WithSettings<T>()`, it stays in code
until you name it. With `WithConfigurableSettings<T>()`, stored settings and runs may change it at once.

- After `WithSettings<T>()` only `Configurable` follows, and after `WithConfigurableSettings<T>()` only
  `NotConfigurable`.
- The generator reads the picks, and their lambdas never run. A pick must be `s => s.Member`
  ([TSH015](./diagnostics.md#tsh015)).
- Obsolete members stay out on their own.
- A configurable member needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)).

::: warning A renamed member loses its stored values
Stored settings keep each member under its name. A configurable member you rename or remove reads as missing, so the
layer below applies. Raise the agent's `WithSettingsVersion` and migrate the stored documents when that matters; see
[the `Settings` record](./generated-members.md#settings).
:::

### Storable types

A configurable member's type is one settings can store ([TSH012](./diagnostics.md#tsh012)):

- strings, Booleans, numbers, enums (stored by name), `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`,
  `TimeOnly` and `Uri`;
- records and classes with a public parameterless constructor whose settable members are storable, replaced as a whole,
  never merged;
- arrays, lists and sets of storable values;
- dictionaries keyed by `string` or an enum.

A type that contains itself is not storable.

## Receiving settings in the tool

The tool takes a parameter of its settings type, and the call fills it with the resolved settings. The parameter stays
out of the tool's schema, so the model never sees it. A tool with settings but no such parameter is
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

[Toolsets](./toolsets.md#parameters) lists the other parameters the call fills.

## The generated overrides

For a tool with configurable members, the generator writes a `{Tool}Overrides` record into the toolset, such as
`TicketToolset.SearchTicketsOverrides`. It holds each configurable member, nullable. Every layer above the code speaks
through it, and null keeps the layer below. [Generated members](./generated-members.md#per-toolset) shows the record in
full.

## The layers

A tool's settings resolve through the four [settings layers](./settings-layering.md): the record's code defaults, the
agent's [`Configure{Tool}()`](./defining-agents.md#tool-settings), the [stored
settings](./settings-and-stores.md#agent-settings) and the [run's settings](./running-agents.md#run-settings). The last
two speak through the overrides, so they reach only the configurable members.

## Related

- [Toolsets](./toolsets.md): declaring the tools that take settings.
- [Settings layering](./settings-layering.md): which layer wins, and what a layer cannot do.
- [Agents](./defining-agents.md#tool-settings): setting a tool's settings in one agent.
- [Builders](./builders.md#toolbuilder): every call of a tool's declaration.
