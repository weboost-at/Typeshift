---
title: Toolsets
description: Group the tools that belong together into a toolset, declare each one as a static method, and let the generator turn it into an AIFunction.
---

# Toolsets

A toolset is a class that groups the tools an agent can call, for example searching, reading and closing support
tickets. You write each tool as a static method with XML docs. Typeshift builds the schema the model reads from the
method's parameters and docs, so there is no schema to keep in step with your code.

## Design your toolsets

You may group tools by feature, by domain or by permission level. How you group them is up to you. An agent offers
whole toolsets and can [include or exclude](./defining-agents.md#selecting-tools) single tools, so a toolset works
best when it holds tools an agent usually offers together. A toolset whose tools only read, for example, suits a
sub-agent that must change nothing. A tool that only one agent needs can also
[live in that agent](./defining-agents.md#add-a-tool-of-the-agent-s-own).

## Declaring tools

A toolset is a `partial` class ([TSH001](./diagnostics.md#tsh001)) deriving from `Toolset`. This one searches
tickets, reads one and closes one, which cannot be undone:

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
| `RequiresApproval()` | The tool's calls need approval before they run, as [Approval and modes](./tool-approval.md) describes. |
| `WithSettings<T>()` | The tool gets [settings](./tool-settings.md) the model never sees, such as a search's limit. Put it last in the chain. `WithConfigurableSettings<T>()` does the same, but opens every settable member to stored settings and runs, except the ones `NotConfigurable` keeps in code. |

`WithDisplayName("Tickets")` names the toolset for people, for example in a tool picker. `AddTool` takes a static method
group, so the generated function can call it directly ([TSH003](./diagnostics.md#tsh003)). Each `AddTool` sits directly
in the constructor ([TSH014](./diagnostics.md#tsh014)), as one chain ([TSH015](./diagnostics.md#tsh015)), and
[How Typeshift reads your code](./how-typeshift-reads-your-code.md) explains why. [Builders](./builders.md#toolbuilder)
lists every call.

## Names and descriptions

A tool is named after its method in snake_case, without `Async`: `GetTicketAsync` becomes `get_ticket`.
`WithName(...)` picks another name. It must be lower_snake_case ([TSH008](./diagnostics.md#tsh008)) and unique
across the toolsets of your project and of the projects it references ([TSH007](./diagnostics.md#tsh007)).

The description matters more than the name. The model reads it to decide when to call the tool, and reads each
`<param>` to fill in the arguments. It comes from the method's `<summary>` or from `WithDescription(...)`, and the
analyzer warns about a tool with neither ([TSH009](./diagnostics.md#tsh009)). The package turns on
`GenerateDocumentationFile`, which your XML docs need to reach the generator, unless your project turns it off
([TSH011](./diagnostics.md#tsh011)).

::: warning Renaming a method renames its tool
Stored settings are keyed by the tool's name. Rename `SearchTickets`, and what your customers stored for
`search_tickets` stops applying. Pin the old name with `WithName` before you rename.
:::

## Parameters

Parameters become the tool's JSON schema, with their `<param>` docs and default values. For `search_tickets` above,
the model receives this schema next to the tool's name and description:

```json
{
  "type": "object",
  "properties": {
    "query": {
      "description": "Words the ticket's title contains.",
      "type": "string"
    }
  },
  "required": [
    "query"
  ]
}
```

The description of `query` is its `<param>` text. `settings` and `services` are missing from the schema, because
Typeshift fills four kinds of parameters itself when it calls the tool. The model never sees them, so they need no
doc:

| Type | Receives |
|---|---|
| The record of `WithSettings<T>()` or `WithConfigurableSettings<T>()` | The tool's resolved settings. |
| `IServiceProvider` | The run's services: the scope the host was resolved from. |
| `CancellationToken` | The call's token. |
| `AIFunctionArguments` | The raw arguments the model sent. |

The `IServiceProvider` is the scope you resolved the agent's host from. So when you resolve the host in a web
request, every tool of the run reaches that request's scoped services, such as the current customer. A nullable
parameter of these types is filled the same way, so `CancellationToken? ct = null` receives the call's token.

- An optional argument the model leaves out takes its default, and so does one it sends as null. A nullable
  reference parameter accepts null in the schema, but the model must still send it unless it has a default value. A
  reference parameter that defaults to null accepts null as well, also without the `?`, as in a project without
  nullable annotations.
- The properties of a record or class parameter are described by their XML docs, a positional record's `<param>`
  docs included. Use them to tell the model how to fill a property, such as the format of a code.
  [Registration](./registration.md#xml-docs-of-parameter-types) shows how those docs are collected.
- The analyzer rejects what JSON cannot fill: `ref` and `out` parameters, pointers, ref structs such as `Span<T>`
  and `dynamic` ([TSH004](./diagnostics.md#tsh004)). Take a `JsonElement` where the JSON may have any shape.

### Arguments that do not fit

A model corrects a call only when it reads what was wrong with it. So a call whose arguments do not fit its
parameters fails before your method runs, with a [`ToolException`](#when-a-tool-fails) whose message the model
reads:

- A required argument the model leaves out fails the call, naming the tool and the argument.
- A null for a required argument fails the same way, so a tool never acts on `0` where the model knew no number.
  A parameter that takes null receives it: a nullable type, such as `int?` or `string?`, or a
  [union](#union-types). In a project without nullable annotations no reference parameter is nullable.
- A value of the wrong type fails naming the argument, the tool and what the JSON reader made of the value, as in
  "The argument 'number' of the tool 'close_ticket' does not fit its schema: The JSON value could not be converted
  to System.Int32."
- Inside a record or class argument, a member without a default that the model leaves out fails the call, and so
  does a null for a member whose type takes none. The message names the member, as the schema names it as required.
- Some clients send an object or a list as a JSON string. Typeshift reads such a string as the JSON it holds,
  unless the parameter takes the string itself.

## Results

A tool returns a value, nothing (`void`), `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` or `IAsyncEnumerable<T>`.
The model receives the result as JSON, serialized through its type's `JsonTypeInfo`, so in an app published with
[Native AOT or trimmed](./native-aot.md#tool-parameter-and-result-types) that type needs JSON metadata. The model
takes one result per call, so Typeshift reads a stream to its end and sends its items as one array, and such an app
needs the metadata of that list. A cancelled call stops reading the stream at its next item, also when the method takes no
`CancellationToken`.

The analyzer rejects a result JSON cannot carry: a pointer, a ref struct or `dynamic`
([TSH004](./diagnostics.md#tsh004)). It also rejects a task or a stream that may itself be null, such as
`Task<string>?`. Return the task always and make its value nullable, as in `Task<string?>`.

## When a tool fails

A tool tells the model what went wrong by throwing a `ToolException`. The model reads its message as the result of
the call, so write it as a hint that says what to do instead, and the model can correct its call:

<!-- snippet: tool-exception -->
<a id='snippet-tool-exception'></a>
```cs
internal partial class TicketEscalationToolset : Toolset
{
    public TicketEscalationToolset() => AddTool(EscalateTicketAsync);

    /// <summary>
    /// Hands an open ticket to a person of the support team.
    /// </summary>
    /// <param name="number">The ticket's number.</param>
    public static async Task<string> EscalateTicketAsync(int number, IServiceProvider services, CancellationToken ct)
    {
        var ticket = await services.GetRequiredService<ITicketClient>().GetAsync(number, ct)
            ?? throw new ToolException($"No ticket {number} exists. Search for it first.");

        if (ticket.Closed)
            throw new ToolException($"Ticket {number} is closed. Tell the customer to open a new one.");

        return $"Ticket {number} is with the support team now.";
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketEscalationToolset.cs#L5-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-tool-exception' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- For ticket 4711 the model reads "Error: Function failed. Exception: No ticket 4711 exists. Search for it first."
  and can search before it tries again.
- The message goes where a tool's result goes: to the model provider, into the response and into the stored
  conversation. Keep out of it what those may not hold.
- Derive exceptions of your own from `ToolException` where your code catches them by type.
- What Typeshift itself tells the model is a `ToolException` too: [arguments that do not fit](#arguments-that-do-not-fit)
  and a sub-agent's answer [outside its result type](./multi-agent.md#typed-answers).

Of any other exception the model reads one fixed sentence: "Error: Function failed. The details of the failure are
not available to you." A database driver, an HTTP client or the container wrote such a message for a developer, and
it may hold a server's name, a query or a connection string. It stays in your app, where you find it:

- The tool loop logs the exception as an error, under the category
  `Microsoft.Extensions.AI.FunctionInvokingChatClient`, when your app registers logging.
- The call's `execute_tool` span carries its type and its message ([Spans](./observability.md#spans)).
- In the run's response, the call's `FunctionResultContent` holds the exception in its `Exception` property, which
  is never serialized. An app without logging and tracing, such as a console app on
  [`TypeshiftRuntime.Create`](./registration.md#the-runtime), reads it there.
- A [tool middleware](./middleware.md#wrap-every-tool-call) sees it first, and can throw a `ToolException` in its
  place, for example for a timeout the model may retry.

Three rounds of tool calls in a row may fail, whatever they threw. A fourth failing round ends the run, and the
exception the tool threw reaches your code as it is.
[`WithMaxConsecutiveToolErrors`](./builders.md#runoptionsbuilder) sets another limit.

## Union types

A tool can take or return a C# 15 union. The model sees a schema for each case and sends the JSON of the
case it picks, and Typeshift binds the case that JSON fits:

<!-- snippet: union-parameter -->
<a id='snippet-union-parameter'></a>
```cs
internal union TicketKey(int, string);

internal partial class TicketLookupToolset : Toolset
{
    public TicketLookupToolset()
    {
        AddTool(FindTicketAsync)
            .ReadOnly();
    }

    /// <summary>
    /// Finds a ticket by its number, or by words its title contains.
    /// </summary>
    /// <param name="key">The ticket's number, or words its title contains.</param>
    public static async Task<Ticket?> FindTicketAsync(TicketKey key, IServiceProvider services, CancellationToken ct)
    {
        var tickets = services.GetRequiredService<ITicketClient>();

        return key switch
        {
            int number => await tickets.GetAsync(number, ct),
            string words => tickets.Search(words, includeClosed: true, limit: 1).FirstOrDefault(),
            null => null
        };
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketLookupToolset.cs#L5-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-union-parameter' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- A JSON string binds the `string` case and a number the `int` case. A string that holds a number, such as `"42"`,
  binds a number case when no case reads a JSON string, as it would for an `int` parameter.
- An object binds the case whose properties it carries, so a union of two records needs no attribute.
- JSON `null` binds the empty union, whose `Value` is null. That is why the switch has a `null` arm.
- Some unions JSON cannot tell apart: two cases that read a string, such as a `Guid` and a `string`, or two records
  where one has every property of the other. A call with such a value fails, and the model reads which cases it
  could have taken. Give the union a classifier of its own with `[JsonUnion]`, as the
  [System.Text.Json docs](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/union-types)
  show, and Typeshift uses it.
- A result reaches the model as the JSON of its case, with nothing around it.
- A union is no [storable type](./tool-settings.md#storable-types), so it cannot be a configurable setting.

## What a toolset cannot be

A toolset derives from `Toolset` directly and has no type parameters ([TSH017](./diagnostics.md#tsh017)). Share code
between toolsets through static methods rather than a base class. A toolset is no `file` class either, because
[generated code sits in files of its own](./how-typeshift-reads-your-code.md#generated-code-sits-in-files-of-its-own).

Typeshift creates each toolset once, with `new`, so the class needs a public or internal parameterless constructor
and cannot be nested as `private` or `protected` ([TSH016](./diagnostics.md#tsh016)). Its constructor therefore takes
no services. A tool reaches them through its [`IServiceProvider` parameter](#parameters).

## Related

- [Tool settings](./tool-settings.md): values a tool needs that the model must not choose.
- [Agents](./defining-agents.md#selecting-tools): offering a toolset's tools to an agent.
- [Builders](./builders.md#toolset): every call a toolset's constructor can make.
- [Generated members](./generated-members.md#per-toolset): what the generator writes for each toolset.
