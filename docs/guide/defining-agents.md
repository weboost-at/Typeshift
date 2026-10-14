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

        ConfigureSearchTickets()
            .WithLimit(5);

        UseMcpServer("status_page").ReadOnly();

        UseContext<CustomerProfileContext>();
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L42' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithName`** is optional; without it the agent goes by its class name in snake_case, `support_agent`. Messages the
  agent writes carry it as their author.
- **`WithDescription`** says what the agent does.
- **`WithChatOptions()`** sets the options every run starts from: `WithModel`, `WithTemperature`,
  `WithMaxOutputTokens` and one `With...` per other option. `WithTools` adds tools of no toolset, such as a hosted
  web search.
- **`GetInstructions()`** returns the instructions. It runs whenever a run's options are resolved, so the text may be
  computed.

`WithName` and `WithDescription` take constants ([TSH002](./diagnostics.md#tsh002)). Every declaration goes directly
in the constructor, one chain per statement ([TSH014](./diagnostics.md#tsh014),
[TSH015](./diagnostics.md#tsh015)). An agent derives from `Agent` directly ([TSH017](./diagnostics.md#tsh017)); share
declarations through plain static methods and constants. [Builders](./builders.md#agent) lists every call.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only some, in that order, or `Exclude` to
offer all but some:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L12-L14' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The lambda picks from a list the generator writes per toolset, one property per tool: `search_tickets` is
`t.SearchTickets`. A tool of another toolset is not on the list, so picking it does not compile. Use each toolset
once and pick each tool once ([TSH025](./diagnostics.md#tsh025)). A toolset of another project needs the package in
that project ([TSH019](./diagnostics.md#tsh019)).

## Other agents

`UseAgent<T>()` offers another agent as a tool, `UseHandoff<T>()` lets the model hand the conversation to it. See
[multiple agents](./multi-agent.md).

## Context providers

`UseContext<T>()` adds what a provider returns to every run: instructions, messages ahead of the input, tools. See
[context providers](./context-providers.md).

## Configurable chat options

`Configurable(o => o.Temperature)` marks an option that a run may change, whether the code sets it or not. The model
is an option like any other: `Configurable(o => o.ModelId)` lets settings choose it, so a settings form never offers
a model the agent does not allow. [Builders](./builders.md#chatoptionsbuilder) lists the options that may be
configurable.

## Run options

`WithRunOptions()` sets how a run's tool loop goes: the [mode](./modes.md) it starts in, its limits, and when the
history is [compacted](./long-running-agents.md#compaction). `Configurable` marks those a run may change;
[builders](./builders.md#runoptionsbuilder) lists them with their defaults:

<!-- snippet: run-options -->
<a id='snippet-run-options'></a>
```cs
WithRunOptions()
    .WithMaxToolIterations(10)
    .WithConcurrentToolCalls(true)
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-run-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Tool settings

`Configure{Tool}()` sets a tool's settings in this agent, over the record's code defaults:
`ConfigureSearchTickets().WithLimit(5)`. The generator writes one per offered tool with settings, with a `With...`
per settable member, configurable or not; see [generated members](./generated-members.md#configure-tool). A tool the
agent does not offer has none, so configuring it does not compile.

## The generated settings

Every agent gets a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may change at runtime: the
model and the other configurable chat options, the configurable run options, and the overrides of each offered tool
with configurable settings. [Generated members](./generated-members.md#settings) lists its members and its JSON.

Store it [under a settings id](./settings-and-stores.md#agent-settings), pass it
[for one run](./running-agents.md#run-settings), or bind a settings form or an API to it. `WithStorageKind` and
`WithSettingsVersion` name and version it in a store.
