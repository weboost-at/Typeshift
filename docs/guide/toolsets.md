# Toolsets

A toolset groups the tools of one domain. The generator reads its constructor at compile time and writes, per tool,
an `AIFunction` subclass that binds the model's arguments and serializes the result without reflection.

## Declaring tools

A toolset is a `partial`, non-generic class deriving from `Toolset`. Each `AddTool` call declares one tool:

<!-- snippet: toolset -->
<a id='snippet-toolset'></a>
```cs
internal partial class TicketToolset : Toolset
{
    public TicketToolset()
    {
        DisplayName = "Tickets";

        AddTool(SearchTickets)
            .WithName("search_tickets")
            .WithSettings<SearchTicketsSettings>()
            .Configurable(s => s.IncludeClosed)
            .Configurable(s => s.Limit);

        AddTool(GetTicketAsync)
            .WithName("get_ticket")
            .WithDescription("Reads one ticket by its number.");
    }

    /// <summary>
    /// Searches the customer's support tickets, newest first.
    /// </summary>
    /// <param name="query">Words the ticket's title contains.</param>
    /// <param name="settings">The settings the search runs with; the model never sees them.</param>
    /// <param name="services">The services of the call.</param>
    public static IReadOnlyList<Ticket> SearchTickets(
        string query,
        SearchTicketsSettings settings,
        IServiceProvider services) =>
        services.GetRequiredService<ITicketClient>().Search(query, settings.IncludeClosed, settings.Limit);

    /// <param name="number">The ticket's number.</param>
    /// <param name="services">The services of the call.</param>
    /// <param name="ct">Cancelled when the call is.</param>
    public static Task<Ticket?> GetTicketAsync(int number, IServiceProvider services, CancellationToken ct) =>
        services.GetRequiredService<ITicketClient>().GetAsync(number, ct);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L6-L46' title='Snippet source file'>snippet source</a> | <a href='#snippet-toolset' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **The method** is a static method group, `AddTool(SearchTickets)`. Lambdas, instance methods and local functions
  are reported as [TSH004](./diagnostics.md#tsh004).
- **`WithName`** is required ([TSH003](./diagnostics.md#tsh003)), a constant ([TSH002](./diagnostics.md#tsh002)),
  lower_snake_case ([TSH012](./diagnostics.md#tsh012)) and unique across everything the host can see
  ([TSH007](./diagnostics.md#tsh007)). The name, not the method, is the tool's identity: stored settings and agents
  refer to it.
- **The description** comes from `WithDescription(...)`, or else from the method's `<summary>`. A tool without
  either is reported as [TSH008](./diagnostics.md#tsh008); summaries need `GenerateDocumentationFile`
  ([TSH010](./diagnostics.md#tsh010)).
- **`WithSettings<T>()`** gives the tool a settings record whose members stay in code unless `Configurable(...)` names
  them; **`WithConfigurableSettings<T>()`** one whose members may all change unless `NotConfigurable(...)` names them.
  See [tool settings](./tool-settings.md). Either comes last in the chain.
- **`DisplayName`** is an optional name for people, in a tool picker for example.

## Parameters

Every parameter becomes part of the tool's JSON schema, with its `<param>` doc as description and its default value,
except these, which the call fills:

| Type | Receives |
|---|---|
| The settings record of `WithSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The services of the call: a DI scope of its own per call. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

A missing required argument fails the call with an `ArgumentException`, which the tool loop reports to the model; an
absent optional argument takes its default. A nullable reference parameter is typed as nullable in the schema.

## Results

A tool may return a value, `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`. The result is serialized through
`JsonTypeInfo`, so under Native AOT a `JsonSerializerContext` of the toolset's assembly must list its type, which
`AddTypeshift()` then registers; see [Native AOT](./native-aot.md).

## Tool references

The generator adds a nested `Tools` class with a typed reference per tool, `TicketToolset.Tools.SearchTickets`. APIs
that address a single tool take these references, so the settings and overrides types follow from them:
[tool defaults](./persistence.md#tool-defaults) and `ToolCatalog.CreateTool`, which creates any registered tool by
name for agents a host assembles itself.

## Generic toolsets

Generic toolsets are not generated: their tools depend on type arguments only the agent knows. Using one throws an
`InvalidOperationException` when an agent asks it for a tool.
