---
title: Toolsets
description: Declare the tools of one domain as static methods, and let the generator turn each one into an AIFunction.
---

# Toolsets

A toolset holds the tools of one domain, such as your support tickets. Each tool is a static method, and the generator
turns it into an `AIFunction` at compile time, without reflection.

## Declaring tools

A toolset is a `partial` class deriving from `Toolset`. This one searches tickets, reads one, and closes one, which
cannot be undone:

<!-- snippet: toolset -->
<a id='snippet-toolset'></a>
```cs
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        WithDisplayName("Tickets");

        AddTool(SearchTickets)
            .ReadOnly()
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);

        AddTool(GetTicketAsync)
            .WithDescription("Reads one ticket by its number.")
            .ReadOnly();

        AddTool(CloseTicketAsync)
            .RequiresApproval();
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);

    /// <param name="number">The ticket's number.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);

    /// <summary>
    /// Closes a ticket for good.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    /// <param name="reason">Why it is closed, as the customer will read it.</param>
    public static Task CloseTicketAsync(int number, string reason, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().CloseAsync(number, reason, ct);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L5-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The calls chained onto `AddTool` describe the tool:

| Call | Means |
|---|---|
| `ReadOnly()` | The tool changes nothing. [Plan mode](./tool-approval.md#plan-mode) offers only these. |
| `RequiresApproval()` | A run that calls the tool waits until the host decides. See [Tool approval](./tool-approval.md). |
| `WithSettings<T>()` | The tool gets [settings](./tool-settings.md) the model never sees, such as a search's limit. It comes last; `WithConfigurableSettings<T>()` works the same way. |
| `WithDisplayName(...)` | On the toolset: a name for people, for example in a tool picker. |

`AddTool` takes a static method group, so the generated function can call it directly
([TSH003](./diagnostics.md#tsh003)). The generator reads these chains from your source and never runs the
constructor. That is why each `AddTool` sits directly in the constructor ([TSH014](./diagnostics.md#tsh014)), as one
chain ([TSH015](./diagnostics.md#tsh015)). [Builders](./builders.md#toolbuilder) lists every call.

## Names and descriptions

A tool is named after its method in snake_case, without `Async`: `GetTicketAsync` becomes `get_ticket`.
`WithName(...)` picks another name, which must be lower_snake_case ([TSH008](./diagnostics.md#tsh008)) and unique
across the host ([TSH007](./diagnostics.md#tsh007)).

The description matters more than the name. The model reads it to decide when to call the tool, and reads each
`<param>` to fill in the arguments. It comes from the method's `<summary>` or from `WithDescription(...)`, and a
tool with neither is [TSH009](./diagnostics.md#tsh009). Your XML docs reach the generator only when the project sets
`GenerateDocumentationFile` ([TSH011](./diagnostics.md#tsh011)).

::: warning Renaming a method renames its tool
Stored settings are keyed by the tool's name. Rename `SearchTickets`, and what your customers stored for
`search_tickets` stops applying. Pin the old name with `WithName` before you rename.
:::

## Parameters

Parameters become the tool's JSON schema, with their `<param>` docs and default values. Four kinds are filled by the
call instead. The model never sees them, and they need no doc:

| Type | Receives |
|---|---|
| The record of `WithSettings<T>()` or `WithConfigurableSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The run's services: the scope the host was resolved from. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

Because the services are the host's scope, a web request's scoped services, such as the current customer, reach
every tool of its run.

- A required argument the model leaves out fails the call with an `ArgumentException`. The tool loop reports it
  back to the model.
- An optional argument the model leaves out takes its default. A nullable reference parameter is nullable in the
  schema.
- The properties of a record or class parameter are described by their XML docs, a positional record's `<param>`
  docs included. Tell the model there how to fill a property, such as the format of a code.
  [Registration](./registration.md#xml-docs-of-parameter-types) shows how those docs are collected.
- `ref` and `out` parameters are [TSH004](./diagnostics.md#tsh004).

## Results

A tool returns a value, `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` or `IAsyncEnumerable<T>`. The model receives
the result as JSON, serialized through its type's `JsonTypeInfo`, so under [Native AOT](./production.md#native-aot)
that type needs JSON metadata. A model takes one result per call, so a stream is read to its end and reaches it as one
array, a `List<T>` to the JSON metadata.

When a tool throws, the model reads the exception's message. Write it as a hint, such as "No ticket 4711 exists.
Search for it first.", and the model can correct its call. Keep secrets out of it, because the message goes to the
model provider.

## What a toolset cannot be

A toolset derives from `Toolset` directly and has no type parameters ([TSH017](./diagnostics.md#tsh017)). Share code
between toolsets through static methods rather than a base class.

## Related

- [Tool settings](./tool-settings.md): values a tool needs that the model must not choose.
- [Agents](./defining-agents.md#selecting-tools): offering a toolset's tools to an agent.
- [Built-in toolsets](./built-in-toolsets.md): to-dos, files, the shell and the web.
- [Builders](./builders.md#toolset): every call a toolset's constructor can make.
- [Generated members](./generated-members.md#per-toolset): what the generator writes for each toolset.
