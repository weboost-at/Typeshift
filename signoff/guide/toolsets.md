# Toolsets

A toolset groups the tools of one domain. The generator reads its constructor at compile time and writes, per tool,
an `AIFunction` that binds the model's arguments and serializes the result without reflection.

## Declaring tools

A toolset is a `partial`, non-generic class deriving from `Toolset`. Each `AddTool` call declares one tool:

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Tickets/TicketToolset.cs#L5-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **The method** is a public static method group. Lambdas, instance methods and local functions are
  [TSH003](./diagnostics.md#tsh003). `AddTool` calls go directly in the constructor ([TSH014](./diagnostics.md#tsh014)).
- **The name** is the method's in snake_case, without an `Async` suffix: `CloseTicketAsync` is `close_ticket`.
  `WithName(...)` overrides it ([TSH008](./diagnostics.md#tsh008)). Names are unique across everything the host sees
  ([TSH007](./diagnostics.md#tsh007)).
- **The description** is the method's `<summary>`, or `WithDescription(...)`. A tool without either is
  [TSH009](./diagnostics.md#tsh009).
- **`RequiresApproval()`** pauses a run that calls the tool until the host decides; see
  [tool approval](./tool-approval.md).
- **`ReadOnly()`** marks a tool that only reads, so [plan mode](./modes.md#plan-mode) offers it.
- **`WithSettings<T>()`** or **`WithConfigurableSettings<T>()`** come last; see [tool settings](./tool-settings.md).
- **`WithDisplayName`** names the toolset for people, in a tool picker say.

A tool's name keys its stored settings. Renaming the method renames the tool, so pin the old name with `WithName`
before you rename a method whose settings are stored.

## Parameters

Every parameter is part of the tool's JSON schema, with its `<param>` doc and default value, except these, which the
call fills:

| Type | Receives |
|---|---|
| The record of `WithSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The run's services: the scope the host resolved the `AgentHost` from. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

These need no `<param>` doc. A missing required argument fails the call with an `ArgumentException`, which the tool
loop reports to the model; an absent optional one takes its default. `ref` and `out` parameters are
[TSH004](./diagnostics.md#tsh004).

## Results

A tool returns a value, `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`. The result reaches the model as JSON,
serialized through its `JsonTypeInfo`. Outside Native AOT any type works; under Native AOT see
[Native AOT](./native-aot.md).

## What is not generated

Generic toolsets, and toolsets deriving from another toolset, are [TSH017](./diagnostics.md#tsh017). Share code
between toolsets through plain static methods.
