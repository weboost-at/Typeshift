# Agents

An agent bundles what a model needs for one job: chat options, instructions, tools and their settings. Like a
toolset, it is declared in its constructor and read at compile time.

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `Agent`:

<!-- snippet: agent -->
<a id='snippet-agent'></a>
```cs
internal partial class SupportAgent : Agent
{
    public SupportAgent()
    {
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.ModelId)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets).Include(t => t.CloseTicket);

        UseToolset<KnowledgeToolset>().Exclude(t => t.DeleteArticle);

        UseToolset<TodoToolset>();

        UseMcpServer("status_page");

        UseContext<CustomerProfileContext>();

        WithRunOptions()
            .WithMaxInputTokens(60_000)
            .WithSummaryModel("fast-model");

        ConfigureSearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/SupportAgent.cs#L8-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithName`** is optional; without it the agent goes by its class name in snake_case, `support_agent`. Messages the
  agent writes carry it as their author.
- **`WithDescription`** says what the agent does; an agent another agent calls or hands off to needs one.
- **`WithChatOptions()`** sets the options every run starts from: `WithModel`, `WithTemperature`,
  `WithMaxOutputTokens` and one `With...` per other option. `WithTools` adds tools of no toolset, such as a hosted
  web search.
- **`GetInstructions()`** returns the instructions. It runs whenever a run's options are resolved, so the text may be
  computed.
- **`WithRunOptions()`** sets the [mode](./modes.md), the tool loop's limits and compaction; see
  [long-running agents](./long-running-agents.md).
- The rest of the chain adds capabilities: [MCP servers](./mcp.md), [context](./context-providers.md),
  [other agents](./multi-agent.md).

An agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)); share declarations through plain static
methods and constants.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only some, in that order, or `Exclude` to
offer all but some:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The lambda picks from a list the generator writes per toolset, one property per tool: `search_tickets` is
`t.SearchTickets`. A tool of another toolset is not on the list, so picking it does not compile. Keep the chain in
one expression; a split chain is [TSH015](./diagnostics.md#tsh015). Offering a tool twice is
[TSH025](./diagnostics.md#tsh025).

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that stored settings or a run may change, whether the code sets it
or not. The model is an option like any other: `Configurable(o => o.ModelId)` lets settings choose it, so a settings
form never offers a model the agent does not allow.

## Tool settings

`Configure{Tool}()` sets a tool's settings in this agent, over the record's code defaults:
`ConfigureSearchTickets().WithLimit(5)`. The generator writes one per offered tool with settings, with a `With...`
per settable member, configurable or not. A tool the agent does not offer has none, so configuring it does not
compile.

## The generated settings

Every agent gets a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may change at runtime:

| Member | From |
|---|---|
| `Model` | When `Configurable(o => o.ModelId)` allows it; overrides `WithModel`. |
| One per configurable chat option | `Instructions` and `Temperature` above. |
| One per configurable run option | `MaxToolIterations` of the [coding agent](./long-running-agents.md#run-options). |
| One per offered tool with configurable settings | `SearchTickets`, `SearchArticles`: their [overrides](./tool-settings.md#the-generated-overrides). |

Every value is nullable, and null keeps the layer below. The record is the agent's settings contract: store it
[under a settings id](./settings-and-stores.md#agent-settings), pass it [for one run](./running-agents.md#run-settings), or bind a
settings form or an API to it. Its JSON is the same through `Serialize()` and through `System.Text.Json`.
