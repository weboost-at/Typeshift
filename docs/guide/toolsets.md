---
title: Toolsets
description: Declare the tools of one domain as static methods, and let the generator turn each one into an AIFunction.
---

# Toolsets

A toolset groups the tools of one domain, such as a customer's support tickets. Each tool is a static method. The
generator reads the toolset's constructor at compile time and writes an `AIFunction` per tool, which binds the
model's arguments and serializes the result without reflection.

::: tip The short version

- A toolset is a `partial` class deriving from `Toolset`. Its constructor declares each tool with `AddTool`.
- A tool is a static method. Its name is the method's in snake_case, its description the method's `<summary>`.
- The parameters become the tool's JSON schema, except the settings, the services, the token and the raw arguments,
  which the call fills.
- `ReadOnly()` and `RequiresApproval()` say what a tool may do; `WithSettings<T>()` gives it settings.

:::

## Declaring tools

A toolset is a `partial`, non-generic class deriving from `Toolset`. Each `AddTool` call in its constructor declares
one tool:

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

- **`AddTool(SearchTickets)`** takes a static method group. Lambdas, instance methods and local functions are
  [TSH003](./diagnostics.md#tsh003): the generated function calls the method directly, without reflection.
- **`ReadOnly()`** marks a tool that only reads and has no side effects. [Plan mode](./modes.md#plan-mode) offers
  only these.
- **`RequiresApproval()`** pauses a run that calls the tool until the host decides. See
  [Tool approval](./tool-approval.md).
- **`WithSettings<T>()`** or **`WithConfigurableSettings<T>()`** gives the tool settings the model never sees. It
  comes last in the chain. See [Tool settings](./tool-settings.md).
- **`WithDisplayName(...)`** names the toolset for people, for example in a tool picker.

The generator reads these chains as you write them. So each `AddTool` sits directly in the constructor
([TSH014](./diagnostics.md#tsh014)), and each declaration is one chain ([TSH015](./diagnostics.md#tsh015)).
[Builders](./builders.md#toolbuilder) lists every call.

## Names and descriptions

A tool's name is its method's name in snake_case, without an `Async` suffix: `GetTicketAsync` becomes `get_ticket`.
`WithName(...)` sets another name, in lower_snake_case ([TSH008](./diagnostics.md#tsh008)). Names are unique across
every tool the host sees ([TSH007](./diagnostics.md#tsh007)).

A tool's description is its method's `<summary>`, or what `WithDescription(...)` sets. The model reads it to decide
when to call the tool, and reads each `<param>` to fill the arguments. A tool without a description is
[TSH009](./diagnostics.md#tsh009). Summaries need `GenerateDocumentationFile` in the project
([TSH011](./diagnostics.md#tsh011)).

::: warning Renaming a method renames its tool
A tool's name keys its stored settings. Rename the method, and the settings stored under the old name no longer
apply. Pin the old name with `WithName` before you rename a method whose settings are stored.
:::

## Parameters

Every parameter is part of the tool's JSON schema, with its `<param>` doc and its default value. The call fills
these four itself, and leaves them out of the schema:

| Type | Receives |
|---|---|
| The record of `WithSettings<T>()` or `WithConfigurableSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The run's services: the scope the host was resolved from. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

They need no `<param>` doc. Resolve services from the `IServiceProvider` parameter: since it is the run's scope, a
request's scoped services, such as the current customer, reach the tool.

- A required argument the model leaves out fails the call with an `ArgumentException`. The tool loop reports it to
  the model.
- An optional argument the model leaves out takes its default.
- A nullable reference parameter is nullable in the schema.
- `ref` and `out` parameters are [TSH004](./diagnostics.md#tsh004).

## Results

A tool returns a value, `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>`. The result reaches the model as JSON,
serialized through its type's `JsonTypeInfo`. Under Native AOT, that type needs JSON metadata; see
[Native AOT](./native-aot.md).

A tool that throws fails the call, and the model reads the exception's message. Say in it what the model should do
instead, so it can correct the call, and keep secrets out of it: the provider receives it.

## What a toolset cannot be

A toolset is not generic and derives from `Toolset` directly. A generic toolset, or one deriving from another
toolset, is [TSH017](./diagnostics.md#tsh017). Share code between toolsets through plain static methods.

## Related

- [Tool settings](./tool-settings.md): values a tool needs that the model must not choose.
- [Agents](./defining-agents.md#selecting-tools): offering a toolset's tools to an agent.
- [Built-in toolsets](./built-in-toolsets.md): to-dos, files, the shell and the web.
- [Builders](./builders.md#toolset): every call a toolset's constructor can make.
- [Generated members](./generated-members.md#per-toolset): what the generator writes for each toolset.
