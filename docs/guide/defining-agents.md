---
title: Agents
description: Declare an agent in its constructor, with its chat options, instructions, tools and their settings.
---

# Agents

An agent bundles what a model needs for one job: chat options, instructions and tools. You declare it in its
constructor, and the generator gives it a `Settings` record of what may change at runtime.

## The smallest agent

The smallest agent declares only its instructions. It has no tools and names no model, so the client you run it on
answers with its own model ([Models and providers](./models.md)):

<!-- snippet: smallest-agent -->
<a id='snippet-smallest-agent'></a>
```cs
internal partial class AssistantAgent : Agent
{
    public AssistantAgent() => WithInstructions("Answer the customer's question in one friendly sentence.");
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/AssistantAgent.cs#L3-L8' title='Snippet source file'>snippet source</a> | <a href='#snippet-smallest-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A console app runs it with the `IChatClient` of your provider, through a host from the
[runtime](./registration.md#the-runtime):

<!-- snippet: smallest-agent-run -->
<a id='snippet-smallest-agent-run'></a>
```cs
await using var runtime = TypeshiftRuntime.Create(client);
await using var assistant = runtime.CreateHost<AssistantAgent>();

var answer = await assistant.RunAsync("Where is my refund?");

Console.WriteLine(answer.Text);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/SmallestAgent.cs#L12-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-smallest-agent-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

```text
Your refund was approved and reaches your account within five days.
```

## Declaring an agent

An agent is a `partial` class ([TSH001](./diagnostics.md#tsh001)) deriving from `Agent`. This one answers customers
from their tickets and the help center:

<!-- snippet: agent -->
<a id='snippet-agent'></a>
```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>()
            .Include(t => t.SearchTickets)
            .Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>()
            .Exclude(t => t.DeleteArticle);

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page")
            .ReadOnly();

        UseContext<CustomerProfileContext>();

        UseQuestions();
    }

    protected override string GetInstructions(IServiceProvider services)
    {
        var customer = services.GetRequiredService<ICurrentCustomer>();
        return $"Answer {customer.Name} from their tickets and cite help center articles.";
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L8-L54' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The tools and their settings have sections of their own below. The other calls of this agent declare the rest:

| Call | Declares |
|---|---|
| `WithDescription(...)` | What the agent does, for the model of an agent that calls this one. |
| `WithChatOptions()` | The chat options every run starts from, such as the model and the temperature. [Models and providers](./models.md) explains what `WithModel` names and which client serves it. |
| `UseMcpServer(...)` | The tools of an [MCP server](./mcp.md). `ReadOnly()` lets plan mode offer them. |
| `UseContext<T>()` | A [context provider](./middleware.md#context-providers), whose instructions, messages and tools join every run. |
| `UseQuestions()` | That the model may [ask the user](./tool-approval.md#asking-the-user) a question, which your app answers. |
| `GetInstructions(services)` | The instructions, the agent's system prompt, built for each run from the run's services ([Instructions built per run](#instructions-built-per-run)). `WithInstructions(...)` in the constructor suits instructions that are the same for every run. |

[Builders](./builders.md#agent) lists every call an agent's constructor can make, with what each takes. Among them are
`WithName`, for an agent that is not to be named after its class, and `UseAgent<T>()` and `UseHandoff<T>()`, which
offer [other agents](./multi-agent.md).

`WithName` and `WithDescription` take constants ([TSH002](./diagnostics.md#tsh002)), and every declaration sits directly
in the constructor as one chain ([TSH014](./diagnostics.md#tsh014), [TSH015](./diagnostics.md#tsh015)). An agent derives
from `Agent` directly, never from a base agent of yours ([TSH017](./diagnostics.md#tsh017)). To share between agents,
put values in constants and code in static methods, and keep every declaration in each agent's constructor.
[How Typeshift reads your code](./how-typeshift-reads-your-code.md) explains these rules, and why you cannot define an
agent at runtime.

Typeshift creates each agent once, with `new`, so the class needs a public or internal parameterless constructor and
cannot be nested as `private` or `protected` ([TSH016](./diagnostics.md#tsh016)). Its constructor therefore takes no
services. The agent's tools reach them through their [`IServiceProvider` parameter](./toolsets.md#parameters).

## Instructions built per run

Instructions that depend on the run, such as the customer it serves, come from an override of
`GetInstructions(IServiceProvider services)`, as in the `SupportAgent` above. Typeshift calls it each time it resolves
a run's options, with the run's services: those of the host's scope, which the agent's tools and context providers
resolve from too. One instance of the agent serves every run, so the override reads what varies from `services` and
never from a field of the agent.

- Stored or run settings that set `Instructions` replace what it returns, when the agent makes them configurable
  ([Settings layering](./settings-and-stores.md#settings-layering)).
- A [context provider](./middleware.md#context-providers) appends instructions after the agent's own. It suits
  context that several agents share, and it sees the run's messages, which the override does not.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only the tools you pick, in the order you
pick them, or `Exclude` to offer all but those:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>()
    .Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L15-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The lambda picks from a list the generator writes for each toolset, with one property per tool, named after the
tool: `search_tickets` is `t.SearchTickets`, and `close_ticket`, from `CloseTicketAsync`, is `t.CloseTicket`. A tool
of another toolset is not on that list, so picking it does not compile. Use each toolset once and pick each tool once
([TSH025](./diagnostics.md#tsh025)). A toolset from another project needs the Typeshift package in that project
([TSH019](./diagnostics.md#tsh019)).

The same use can make a tool [ask your app first](./tool-approval.md#requiring-approval) with
`RequiresApproval(t => t.X)`, in this agent only.

## Switching a tool off

`Configurable(t => t.X)` on a use lets stored settings or a run switch the tool off, so one customer's runs offer
it and another's do not:

<!-- snippet: switchable-tool -->
<a id='snippet-switchable-tool'></a>
```cs
UseToolset<TicketToolset>()
    .Include(t => t.SearchTickets)
    .Include(t => t.GetTicket)
    .Configurable(t => t.SearchTickets);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/ReviewDeskAgent.cs#L20-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-switchable-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The agent's `Settings` gain `bool? SearchTicketsEnabled`, named after the tool. False leaves the tool out of the
  run, and null keeps the [layer below](./settings-and-stores.md#settings-layering). With every layer null, the tool
  is offered.
- Only the tools you pick get a switch, as only the options you pick are configurable. The pick names a tool the use
  offers ([TSH043](./diagnostics.md#tsh043)).

Switch it off under a customer's settings id, as the agent's other settings are stored:

<!-- snippet: switch-tool-off -->
<a id='snippet-switch-tool-off'></a>
```cs
await review.WithSettingsId(42).UpdateSettingsAsync(settings => settings with { SearchTicketsEnabled = false });
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/AgentSettings.cs#L59-L61' title='Snippet source file'>snippet source</a> | <a href='#snippet-switch-tool-off' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

That customer's runs no longer offer `search_tickets`, and those of every other customer still do. To switch a tool
off for every customer unless their settings switch it on, store false in the
[agent's own settings](./settings-and-stores.md#an-agent-s-own-settings).

## Add a tool of the agent's own

A tool that only one agent needs can live in the agent itself, without a toolset of its own. Declare it with `AddTool`
in the agent's constructor, backed by a static method of the agent:

<!-- snippet: agent-own-tool -->
<a id='snippet-agent-own-tool'></a>
```cs
internal partial class BillingAgent : Agent
{
    public BillingAgent()
    {
        WithDescription("Settles charges and invoices.");
        WithInstructions("Settle the customer's charges and invoices.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        AddTool(GetToday)
            .ReadOnly();
    }

    /// <summary>
    /// Returns today's date, to tell whether an invoice is overdue.
    /// </summary>
    private static DateOnly GetToday(IServiceProvider services)
    {
        var time = services.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        return DateOnly.FromDateTime(time.GetLocalNow().DateTime);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/BillingAgent.cs#L3-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-own-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The tool takes the same chain as a toolset's tool, such as `ReadOnly()` here or `RequiresApproval()`, and follows
  the same rules for its [name and description](./toolsets.md#names-and-descriptions),
  [parameters](./toolsets.md#parameters) and [results](./toolsets.md#results). So `GetToday` becomes `get_today`, its
  `<summary>` describes it to the model, and the `IServiceProvider` it takes is the run's, which the model never sees.
- The method must be static and may be private, since only the agent's generated code calls it.
- The agent offers its own tools after the tools of its toolsets, in the order it adds them.
- A tool with settings declares them with `WithSettings<T>()` and takes the record as a parameter
  ([Tool settings](./tool-settings.md)). It then gets a `Configure{Tool}()`, and its configurable members join the
  agent's `Settings`, as a toolset's tool does.
- Its name must differ from the tools of the toolsets the agent uses ([TSH025](./diagnostics.md#tsh025)) and from the
  tools of the agents it hands a conversation to or receives one from, directly or through other agents, and of its
  sub-agents and their callers ([TSH007](./diagnostics.md#tsh007)). Other agents may have a tool of the same name.

No other agent can offer this agent's tools, so move the tool into a toolset once a second agent needs it. `Include`
and `Exclude` pick only from toolsets, so the agent always offers its own tools.

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that stored settings and runs may change, whether the code sets it
or not. The model is an option like any other. Without `Configurable(o => o.ModelId)`, stored settings and runs
cannot change it, and the agent's `Settings` has no `Provider` or `Model` for a settings form to offer.

[Builders](./builders.md#chatoptionsbuilder) lists which options may be configurable. The rest belong to the run
rather than to its settings, and the analyzer reports a `Configurable` call that names one of them
([TSH012](./diagnostics.md#tsh012)).

## Run options

`WithRunOptions()` sets how a run's tool loop goes, and `Configurable` marks the options that stored settings and
runs may change:

<!-- snippet: run-options -->
<a id='snippet-run-options'></a>
```cs
WithRunOptions()
    .WithMaxToolIterations(10)
    .WithConcurrentToolCalls(true)
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L20-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Run options set the [mode](./tool-approval.md#the-three-modes) a run starts in, the limits of its tool loop, whether
the calls of one response run at the same time, and when the history is [compacted](./conversations.md#compaction).
[Builders](./builders.md#runoptionsbuilder) lists each one with its default and the range it accepts.

### Tool calls at the same time

`WithConcurrentToolCalls(true)` runs the tool calls of one model response at the same time, which saves time when the
model asks for several lookups at once. Every call runs in the run's scope, the one the host was resolved from, so
the calls share its scoped services. The sub-agents the model calls in one response share them as well, and so does
a [sub-agent in the background](./multi-agent.md#in-the-background), which works beside the calls that follow.

::: warning A `DbContext` serves one call at a time
EF Core [allows one operation at a time](https://learn.microsoft.com/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues)
on a `DbContext`. When two calls query the request's scoped context at once, one of them fails with an
`InvalidOperationException`, or, where EF Core does not notice, they can corrupt the context's state. The model reads
only that the call failed ([When a tool fails](./toolsets.md#when-a-tool-fails)), and whether it happens depends on
which calls the model makes in one response.
:::

So a tool that may run beside another creates what it needs in each call. With EF Core, register the context with
`AddDbContextFactory<T>` and take the factory instead of the context:

<!-- snippet: db-context-factory -->
<a id='snippet-db-context-factory'></a>
```cs
internal partial class OrderToolset : Toolset
{
    public OrderToolset() => AddTool(GetOrderAsync);

    /// <summary>
    /// Reads one of the customer's orders by its number.
    /// </summary>
    /// <param name="number">The order's number.</param>
    public static async Task<Order?> GetOrderAsync(int number, IServiceProvider services, CancellationToken ct)
    {
        var contexts = services.GetRequiredService<IDbContextFactory<ShopDb>>();
        await using var db = await contexts.CreateDbContextAsync(ct);

        return await db.Orders.FindAsync([number], ct);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Orders/OrderToolset.cs#L6-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-db-context-factory' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each call gets a context of its own and disposes it when the call ends. For a service of your own that is not
thread-safe, create a scope in the call with `IServiceScopeFactory` and resolve the service there. That scope holds
new instances of every scoped service, so read what the call needs from the run's services, such as the current
customer, before you create it.

## Tool settings

`Configure{Tool}()` sets a tool's [settings](./tool-settings.md) for every run of this agent, over the record's code
defaults, as `ConfigureSearchTickets().WithLimit(5)` does above.
[Generated members](./generated-members.md#configure-tool) lists what its builder offers and which tools have one.

## The generated settings

Every agent gets a nested `Settings` record, here `SupportAgent.Settings`, holding exactly what may change at
runtime: the chat and run options you marked configurable, the
[overrides](./tool-settings.md#the-generated-overrides) of each offered tool that has configurable settings, and the
switch of each tool a use [lets settings switch off](#switching-a-tool-off). A value you leave null keeps the
[layer below](./settings-and-stores.md#settings-layering).
[Generated members](./generated-members.md#settings) lists the record's members and the declaration each comes from.

Store the record [under a settings id](./settings-and-stores.md#settings-under-an-id-such-as-per-customer), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. The record's `Serialize()`
and `System.Text.Json` write the same JSON, so either works for an API. In a store, `WithStorageKind` sets the name
the record is kept under in place of the class name, and `WithSchemaVersion` raises its schema version after you rename
or remove a configurable member ([Migrate older settings](./implementing-a-store.md#migrate-older-settings)).

## Related

- [Toolsets](./toolsets.md): declaring the tools an agent offers.
- [Tool settings](./tool-settings.md): the record a `Configure{Tool}()` sets.
- [Sub-agents and handoffs](./multi-agent.md): offering another agent as a tool, or handing a conversation to it.
- [Models and providers](./models.md): the client that serves the model an agent names.
- [Running an agent](./running-agents.md): what a run takes and returns.
- [Builders](./builders.md#agent): every call an agent's constructor can make.
- [Generated members](./generated-members.md#per-agent): what the generator writes for each agent.
