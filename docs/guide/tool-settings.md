---
title: Tool settings
description: Give a tool values the model must not choose, such as a result limit, and decide which of them may change at runtime.
---

# Tool settings

Some values a tool needs are not the model's to choose, such as how many tickets a search returns. They go in a settings
record that the tool receives and the model never sees.

## The settings record

A settings record holds the values of one tool, and its parameterless constructor sets the code defaults:

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

It must be a record ([TSH005](./diagnostics.md#tsh005)), because each settings layer applies to a `with` copy of the
defaults.

## Configurable members

Configurable members are an allow list, and you choose which way round it works. `WithSettings<T>()` keeps every
member in code unless `Configurable(...)` names it:

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

`WithConfigurableSettings<T>()` opens every settable member unless `NotConfigurable(...)` names it:

<!-- snippet: settings-all-configurable -->
<a id='snippet-settings-all-configurable'></a>
```cs
AddTool(SearchArticlesAsync)
    .WithConfigurableSettings<SearchArticlesSettings>()
    .NotConfigurable(s => s.Limit);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Knowledge/KnowledgeToolset.cs#L11-L15' title='Snippet source file'>snippet source</a> | <a href='#snippet-settings-all-configurable' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The difference shows when you add a member to the record later. Under `WithSettings<T>()` it stays in code until you
name it. Under `WithConfigurableSettings<T>()`, stored settings and runs can change it at once.

- `WithSettings<T>()` takes only `Configurable` after it, and `WithConfigurableSettings<T>()` only `NotConfigurable`.
- The generator reads the picks and never runs their lambdas, so a pick must be `s => s.Member`
  ([TSH015](./diagnostics.md#tsh015)).
- `WithConfigurableSettings<T>()` leaves obsolete members out on its own.
- A configurable member needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)).

::: warning A renamed member loses its stored values
Stored settings keep each member under its name. Rename or remove a configurable member, and its stored value reads
as missing, so the layer below applies. When that matters, raise the agent's `WithSettingsVersion` and migrate the
stored documents, as [the `Settings` record](./generated-members.md#settings) shows.
:::

### Storable types

A configurable member needs a type settings can store ([TSH012](./diagnostics.md#tsh012)):

- strings, Booleans, enums (stored by name), `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`,
  `TimeOnly`, `Uri` and `ReasoningOptions`;
- the numbers `byte`, `short`, `int`, `long`, `float`, `double` and `decimal`;
- non-generic classes and records with a public parameterless constructor, no `required` members and storable
  settable members, replaced as a whole and never merged;
- arrays, lists and sets of storable values, and dictionaries keyed by `string` or an enum.

Items and dictionary values cannot be nullable value types, and a type that contains itself is not storable.

## Receiving settings in the tool

The tool takes a parameter of its settings type, and the call fills it with the resolved settings. A tool that has
settings but no such parameter is [TSH010](./diagnostics.md#tsh010), since its settings would reach nothing:

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
`TicketToolset.SearchTicketsOverrides`, with each configurable member as nullable. Every layer above the code speaks
through it, and a null keeps the layer below. [Generated members](./generated-members.md#per-toolset) shows it in
full.

## The layers

A tool's settings resolve through four [settings layers](./settings-and-stores.md#settings-layering): the record's
code defaults, the agent's [`Configure{Tool}()`](./defining-agents.md#tool-settings), the
[stored settings](./settings-and-stores.md#agent-settings) and the [run's settings](./running-agents.md#run-settings).
The last two speak through the overrides, which is why they reach only the configurable members.

## Related

- [Toolsets](./toolsets.md): declaring the tools that take settings.
- [Settings layering](./settings-and-stores.md#settings-layering): which layer wins, and what a layer cannot do.
- [Agents](./defining-agents.md#tool-settings): setting a tool's settings in one agent.
- [Builders](./builders.md#toolbuilder): every call of a tool's declaration.
