# Typeshift

Code-defined agents and toolsets for .NET 10 on
[Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai). A source generator turns
them into reflection-free `AIFunction`s, a typed settings record per agent, and one `services.AddTypeshift()` call.

**[Documentation](https://weboost-at.github.io/Typeshift/)** ·
[Diagnostics](https://weboost-at.github.io/Typeshift/guide/diagnostics) ·
[Changelog](CHANGELOG.md)

> **Alpha:** the API may change between releases. [`docs/contributing/status.md`](docs/contributing/status.md) lists
> what is open.

```bash
dotnet add package Typeshift --prerelease
```

## What you get

- Tools from static methods: a generated `AIFunction` each, its schema from the parameters and XML docs.
- Arguments bound and results serialized without reflection, ready for Native AOT.
- Tool settings the model never sees, with configurable members as an allow list.
- A generated `Settings` record per agent: exactly what may change at runtime, the model included.
- Settings layered from code, the agent, a store and the run, under ids you own.
- One `ITypeshiftStore` to implement; without one, everything runs from code.
- Conversations stored step by step: resume after a crash, steer a run, watch it live.
- Tool approval with rules, plan and auto modes, middleware and context providers.
- Sub-agents, handoffs, MCP servers, compaction, OpenTelemetry and built-in toolsets.
- Mistakes as compile errors and analyzer diagnostics, never as errors in generated code.

## A toolset

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

## License

MIT
