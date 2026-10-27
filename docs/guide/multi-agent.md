---
title: Multiple agents
description: Let an agent call another agent as a sub-agent, or hand the conversation to it.
---

# Multiple agents

An agent can work with others in two ways. It calls one as a sub-agent, a tool that answers with text, or it hands the
conversation over to one.

## Sub-agent or handoff

| | Sub-agent | Handoff |
|---|---|---|
| Declared with | `UseAgent<T>()` | `UseHandoff<T>()` |
| The model calls | A tool named like the agent, with one `input` | `handoff_to_{name}` |
| Works with | A fresh context and no conversation of its own | The whole conversation |
| Answers | The calling agent, which gets the text and goes on working | The run, in place of the calling agent |
| Needs a conversation | No | Yes |

## Sub-agents

`UseAgent<T>()` offers another agent as a tool. The tool is named like the agent, described by its
`WithDescription`, and takes one `input` parameter:

<!-- snippet: agent-as-tool -->
<a id='snippet-agent-as-tool'></a>
```cs
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithChatOptions()
            .WithModel("anthropic", "claude-sonnet-5-5");

        UseAgent<ResearchAgent>();
    }

    protected override string GetInstructions() =>
        "Write a market report. Ask research_agent for the companies of each city you cover.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/ReportAgent.cs#L5-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A call runs the sub-agent in a fresh context, with no conversation of its own, and returns its answer's text. It
runs with the caller's settings id, so the settings stored for the sub-agent under that id apply. It also runs in the
caller's mode and shares the caller's [standing approvals](./tool-approval.md#approving-for-the-conversation) and
[approval rules](./tool-approval.md#approval-rules). In [plan mode](./tool-approval.md#plan-mode) it researches with
its read-only tools and answers the caller, which presents the plan.

Its tool calls and tokens stay out of the caller's history, which keeps a long run's context small. A sub-agent can
read twenty files and answer in one paragraph, and only the paragraph reaches the caller. When the calling agent's run
options set `WithConcurrentToolCalls(true)`, the sub-agents the model calls in one response work at the same time.

Run the calling agent like any other:

<!-- snippet: agent-as-tool-run -->
<a id='snippet-agent-as-tool-run'></a>
```cs
var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MultiAgent.cs#L13-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A sub-agent needs a description and a lower_snake_case name ([TSH023](./diagnostics.md#tsh023)), because its name
becomes the tool's and the model reads its description to decide when to call it. The analyzer rejects agents that
call each other in a circle through `UseAgent` ([TSH026](./diagnostics.md#tsh026)), but handoffs may form circles.

::: warning A sub-agent cannot ask for approval
A sub-agent cannot stop to ask your app for approval. In the default mode, each of its calls that needs approval is
rejected unless a standing approval or a rule covers it. The analyzer warns about such tools
([TSH024](./diagnostics.md#tsh024)), so add a rule, or hand off to the agent instead.
:::

### In the background

`InBackground()` starts the sub-agent and returns at once, so the caller goes on working:

<!-- snippet: background-sub-agent -->
<a id='snippet-background-sub-agent'></a>
```cs
UseAgent<ExploreAgent>()
    .InBackground();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/CodingAgent.cs#L19-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-background-sub-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When the sub-agent finishes, its answer reaches the calling run as a message before the run's next model call.

- A run does not end while its sub-agents work. Once the model stops calling tools, the run waits for the next
  answer and continues with it.
- A run that ends some other way, for example with an error, stops the sub-agents still working.
- When a background sub-agent never answered, because the calling run ended first or the process crashed, the
  conversation's next run tells the model it was lost, so the model can call it again.

The sub-agent itself is an agent like any other. This one explores the workspace with a fast model and offers only
the file tools that read, so none of its calls needs approval:

<!-- snippet: sub-agent -->
<a id='snippet-sub-agent'></a>
```cs
internal partial class ExploreAgent : Agent
{
    public ExploreAgent()
    {
        WithDescription("Explores the workspace and answers one question about it, citing files and lines.");

        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        UseToolset<FileSystemToolset>()
            .Include(t => t.ReadFile)
            .Include(t => t.ListFiles)
            .Include(t => t.SearchFiles);
    }

    protected override string GetInstructions() =>
        "Answer the question from the files. Read only what you need; quote the lines you rely on.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/ExploreAgent.cs#L5-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-sub-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Handoff

`UseHandoff<T>()` gives the model a tool, `handoff_to_{name}`, that carries the other agent's description. For
`BillingAgent` it is `handoff_to_billing_agent`. When the model calls
it, the other agent takes over and answers in the same run, with the whole conversation and its own instructions,
tools and stored settings. Like a sub-agent, it needs a description and a lower_snake_case name
([TSH023](./diagnostics.md#tsh023)):

<!-- snippet: handoff -->
<a id='snippet-handoff'></a>
```cs
internal partial class TriageAgent : Agent
{
    public TriageAgent()
    {
        WithChatOptions()
            .WithModel("openai", "gpt-5-mini");

        UseToolset<TicketToolset>()
            .Exclude(t => t.CloseTicket);

        WithRunOptions()
            .WithMaxToolIterations(10)
            .WithConcurrentToolCalls(true)
            .Configurable(r => r.Mode)
            .Configurable(r => r.MaxToolIterations);

        UseHandoff<SupportAgent>();
        UseHandoff<BillingAgent>();
    }

    protected override string GetInstructions() =>
        "Find out what the customer needs, then hand off to support or billing.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/TriageAgent.cs#L5-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-handoff' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The conversation records which agent answered last, and its later runs go to that agent. That holds whichever host
you start them on, as long as that host's agent can reach the last agent through its handoffs, directly or through
other agents' handoffs. Each message carries its agent's name as `AuthorName`:

<!-- snippet: handoff-run -->
<a id='snippet-handoff-run'></a>
```cs
var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
var conversationId = 21;

var first = await triage.RunAsync("I was charged twice.", settingsId, conversationId);
var second = await triage.RunAsync("It was invoice 2024-17.", settingsId, conversationId);

Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MultiAgent.cs#L21-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-handoff-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The `Settings` you pass to one run apply only to the agent you started it on. An agent it hands off to runs with its
own settings stored under the same settings id.

::: warning Handoffs need a conversation
A run without a conversation id does not offer the handoff tools, so its agent cannot hand off.
:::

## Related

- [Agents](./defining-agents.md#declaring-an-agent): declaring the other agents an agent uses.
- [Settings layering](./settings-and-stores.md#settings-layering): which settings sub-agents and handoffs run with.
- [Approval rules](./tool-approval.md#approval-rules): rules that decide a sub-agent's calls.
- [Compaction](./conversations.md#compaction): sub-agents that keep a long run's context small.
- [Conversations](./conversations.md): where a handoff's conversation is stored.
