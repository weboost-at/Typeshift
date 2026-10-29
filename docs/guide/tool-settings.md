---
title: Tool settings
description: Give a tool values the model must not choose, such as a result limit, and decide which of them may change at runtime.
---

# Tool settings

Some values a tool needs are not the model's to choose, such as how many tickets a search returns. They go in a settings
record that the tool receives and the model never sees.

## The settings record

A settings record holds values a tool needs. Its parameterless constructor sets the code defaults, here through
property initializers:

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

It must be a record ([TSH005](./diagnostics.md#tsh005)). Settings resolve in [layers](#the-layers), from these code
defaults up to a single run, and each layer applies its values to a `with` copy of the defaults.

## Configurable members

A configurable member is one that stored settings and runs may change. You choose which members are configurable,
and you can declare that from either side. `WithSettings<T>()` keeps every member in code unless `Configurable(...)`
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
name it. Under `WithConfigurableSettings<T>()`, stored settings and runs can change it right away, without another
code change.

- `WithSettings<T>()` takes only `Configurable` after it, and `WithConfigurableSettings<T>()` only `NotConfigurable`.
- The generator reads each lambda from your source and never runs it, so each one must be a plain member access,
  `s => s.Member` ([TSH015](./diagnostics.md#tsh015)).
- Under `WithConfigurableSettings<T>()`, members marked `[Obsolete]` stay in code without a `NotConfigurable(...)`.
- A configurable member needs a public `set` or `init` accessor ([TSH013](./diagnostics.md#tsh013)).

::: warning A renamed member loses its stored values
Stored settings keep each member under its name. Rename or remove a configurable member, and its stored value reads
as missing, so the layer below applies. When that matters, raise the version the agent passes to
`WithSettingsVersion` and migrate the stored documents, as
[Migrate older settings](./implementing-a-store.md#migrate-older-settings) explains.
:::

### Storable types

A configurable member needs a type settings can store ([TSH012](./diagnostics.md#tsh012)):

- strings, Booleans, enums (stored by name), `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`,
  `TimeOnly`, `Uri` and `ReasoningOptions`;
- the numbers `byte`, `short`, `int`, `long`, `float`, `double` and `decimal`;
- non-generic classes and records with a public parameterless constructor, no `required` members and storable
  settable members, replaced as a whole and never merged, and record structs that meet the same rules;
- arrays, lists and sets of storable values, and dictionaries keyed by `string` or an enum.

Items and dictionary values cannot be nullable value types, and a type that contains itself is not storable. Other
structs are not storable, because most keep their state in fields that no public setter writes.

## Receiving settings in the tool

The tool takes a parameter of its settings type, and Typeshift fills it with the resolved settings when it calls the
tool. The analyzer reports a tool that has settings but no such parameter ([TSH010](./diagnostics.md#tsh010)), since
its settings would reach nothing:

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
`TicketToolset.SearchTicketsOverrides`, with each configurable member as nullable. Stored settings and run settings
set a tool's values through this record, and a null keeps the value of the layer below.
[Generated members](./generated-members.md#per-toolset) shows it in full.

## The layers

A tool's settings resolve through four [settings layers](./settings-and-stores.md#settings-layering): the record's
code defaults, the agent's [`Configure{Tool}()`](./defining-agents.md#tool-settings), the
[stored settings](./settings-and-stores.md#agent-settings) and the [run's settings](./running-agents.md#run-settings).
The last two set values through the overrides, so they can change only the configurable members. The agent's
`Configure{Tool}()` can set every settable member.

## Related

- [Toolsets](./toolsets.md): declaring the tools that take settings.
- [Settings layering](./settings-and-stores.md#settings-layering): which layer wins, and what a layer cannot do.
- [Agents](./defining-agents.md#tool-settings): setting a tool's settings in one agent.
- [Builders](./builders.md#toolbuilder): every call of a tool's declaration.
