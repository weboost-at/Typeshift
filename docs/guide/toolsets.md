# Toolsets

A toolset groups the tools of one domain. The generator reads its constructor at compile time and writes, per tool,
an `AIFunction` that binds the model's arguments and serializes the result without reflection; see
[generated members](./generated-members.md#per-toolset).

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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L5-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`AddTool` takes a static method group, directly in the constructor, one chain per tool. The tool is named after the
method in snake_case without `Async`, `get_ticket`, and described by its `<summary>` and `<param>` docs, so turn on
`GenerateDocumentationFile`. Then refine it:

- `WithDescription` describes a method without a summary.
- `ReadOnly()` marks a tool that only reads, so [plan mode](./modes.md#plan-mode) offers it.
- `RequiresApproval()` asks the host before each call; see [tool approval](./tool-approval.md).
- `WithSettings<T>()` gives it values the model must not choose; see [tool settings](./tool-settings.md).
- `WithDisplayName` names the toolset for people, in a tool picker say.

[Builders](./builders.md#toolbuilder) lists every call and the diagnostics that guard it.

## Renaming a tool

A tool's name keys its stored settings, so renaming the method renames the tool. Pin the old name with `WithName`
before you rename a method whose settings are stored.

## Parameters and results

The model fills every parameter but four, which the call fills: the tool's settings, `IServiceProvider`,
`CancellationToken` and `AIFunctionArguments`. Resolve services from the `IServiceProvider`: it is the run's scope,
so a request's scoped services reach the tool. A tool returns a value, or a task of one, which reaches the model as
JSON. [Builders](./builders.md#tool-parameters) lists the binding rules.

## Sharing code

A generic toolset, or one deriving from another toolset, is [TSH017](./diagnostics.md#tsh017). Share code between
toolsets through plain static methods.
