# Agents

An agent bundles what a model needs for one job: its chat options, the tools it may call and the settings those
tools run with. Like a toolset, it is declared in its constructor and the generator reads it at compile time.

## Declaring an agent

An agent is a `partial`, non-generic class deriving from `AgentDefinition`:

<!-- snippet: agent -->
<a id='snippet-agent'></a>
```cs
internal partial class SupportAgent : AgentDefinition
{
    public SupportAgent()
    {
        WithName("support_agent");
        WithDescription("Answers customers from their tickets and the help center.");

        WithChatOptions()
            .WithModel("fast-model")
            .WithTemperature(0.3f)
            .Configurable(o => o.Instructions)
            .Configurable(o => o.Temperature)
            .Configurable(o => o.MaxOutputTokens);

        UseToolset<TicketToolset>().Include(t => t.SearchTickets);

        UseToolset<KnowledgeToolset>();

        ConfigureSearchTickets()
            .WithLimit(5);
    }

    protected override string GetInstructions() =>
        """
        Answer from the customer's tickets and cite help center articles.
        """;
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/SupportAgent.cs#L7-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- **`WithName`** is optional; without it the agent goes by its class name. The name labels the execution context of
  the agent's conversations. It need not be unique, since nothing is stored by name.
- **`WithDescription`** says what the agent does, for people choosing among agents.
- **`WithChatOptions()`** starts a chain that sets the chat options every run starts from: `WithModel`,
  `WithTemperature`, `WithMaxOutputTokens` and one `With...` per other option. `WithModel` names the model the agent
  runs on unless its settings say otherwise. Tools added with `WithTools`, a hosted web search for example, are
  offered before the toolsets' tools.
- **`GetInstructions()`** is a virtual method returning the agent's instructions, its system prompt. Override it,
  with a raw string literal for anything longer than a line. It runs whenever a run's options are resolved, so the
  instructions may also be computed.

## Selecting tools

`UseToolset<T>()` offers every tool of a toolset. Chain `Include` to offer only some, as the support agent above does
with `search_tickets`, in the order you include them; or `Exclude` to offer all but some:

<!-- snippet: exclude-tools -->
<a id='snippet-exclude-tools'></a>
```cs
internal partial class TriageAgent : AgentDefinition
{
    public TriageAgent()
    {
        WithChatOptions().WithModel("fast-model");

        UseToolset<TicketToolset>().Exclude(t => t.GetTicket);
    }

    protected override string GetInstructions() => "Find the tickets a customer's message is about.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L6-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-exclude-tools' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

One `UseToolset` call either includes or excludes: after `Include` the chain offers only `Include`, after `Exclude`
only `Exclude`. The lambda picks from `t`, a list the generator writes per toolset with one property per tool, named
after the tool: `search_tickets` is `t.SearchTickets`. A tool of another toolset is not on the list, so picking it
does not compile. Keep the chain in one expression; the generator reads it from the syntax. Including one tool twice
throws an `InvalidOperationException` when the agent's tools are first read.

## Configurable chat options

`Configurable(o => o.Temperature)` in the `WithChatOptions()` chain marks a chat option that may change at runtime,
whether the code sets it or not. Like the settings picks, it is read by the generator only. Configurable options must
have a type the settings can store ([TSH014](./diagnostics.md#tsh014)).

## Tool settings

`Configure{Tool}()` sets the settings a tool runs with in this agent, over the tool's code defaults:
`ConfigureSearchTickets().WithLimit(5)`. The generator writes one such method per offered tool that has settings,
with one `With...` per settable member of the tool's settings record, configurable or not. A tool the agent does not
offer has no `Configure` method, so setting its settings does not compile. A tool the agent sets this way no longer
takes its stored defaults; see [the layers](./tool-settings.md#the-layers).

## The generated settings

For every agent, the generator writes a nested `Settings` record, `SupportAgent.Settings`, holding exactly what may
change at runtime:

| Member | From |
|---|---|
| `Model` | Always there; overrides `ChatOptions.ModelId`. |
| One property per configurable chat option | `Instructions`, `Temperature`, `MaxOutputTokens` above. |
| One property per tool with configurable settings | `SearchTickets`, `SearchArticles`: the tools' [overrides](./tool-settings.md#the-generated-overrides). |

Every value is nullable, and null keeps what the layer below sets. The record is the agent's settings contract:
store it [per id](./persistence.md#agent-settings), pass it [for one run](./running-agents.md#run-settings), or bind
a settings UI or an API to it. It serializes itself to JSON, so it needs no serializer setup.

## The runtime

`AddTypeshift()` registers each agent as a singleton together with an `AgentRuntime<TAgent>`, the object a host runs
it through; see [running agents](./running-agents.md). Every runtime is also registered as `AgentRuntime`, so a host
can list all agents.
