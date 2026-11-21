---
title: Sub-agents and handoffs
description: Let an agent call another agent as a sub-agent, or hand the conversation to it.
---

# Sub-agents and handoffs

Split a job across agents when one agent would need too many tools, or instructions too long to follow well. An agent
can call another as a sub-agent, a tool that answers its question, or hand the conversation over to it, so that agent
goes on with the customer.

## Sub-agent or handoff

| | Sub-agent | Handoff |
|---|---|---|
| Declared with | `UseAgent<T>()` | `UseHandoff<T>()` |
| The model calls | A tool named like the agent, with one `input` | `handoff_to_{name}` |
| Works with | A fresh context and no conversation of its own | The whole conversation |
| Answers | The calling agent, which gets the text and goes on working | The run, in place of the calling agent |
| Needs a conversation | No | Yes |

## Sub-agents

`UseAgent<T>()` offers another agent as a tool. The tool has the agent's name and description, and takes one `input`
parameter:

<!-- snippet: agent-as-tool -->
<a id='snippet-agent-as-tool'></a>
```cs
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithInstructions("Write a market report. Ask research_agent for the companies of each city you cover.");

        WithChatOptions()
            .WithProvider("anthropic")
            .WithModel("claude-sonnet-5-5");

        UseAgent<ResearchAgent>();
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/ReportAgent.cs#L5-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A call runs the sub-agent in a fresh context, with no conversation of its own, and returns its answer's text. It
runs with the caller's settings id, so the settings stored for the sub-agent under that id apply. It also runs in the
caller's mode and shares the caller's [standing approvals](./tool-approval.md#approving-for-the-conversation) and
[approval rules](./tool-approval.md#approval-rules). In [plan mode](./tool-approval.md#plan-mode) it researches with
its read-only tools and answers the caller, which presents the plan.

Each agent runs on the client of the provider it names itself. Here `ReportAgent` takes the client registered
under `anthropic`, and `ResearchAgent`, which names no provider, the default provider's
([Registering clients](./models.md#registering-clients)). A provider the call site names with
`reports.WithProvider(...)` serves both, and each keeps its own model
([Choosing at the call site](./models.md#choosing-at-the-call-site)).

The run checks the sub-agents' providers before it stores anything, so a sub-agent whose provider no client serves
fails the run at its start. A sub-agent that fails later, on its model's error for example, fails its call as
[a tool that fails](./toolsets.md#when-a-tool-fails) does. The calling model reads that the call failed, but not
why, the exception is logged and set on the call's span, and the calling run goes on.

Its tool calls and tokens stay out of the caller's history, which keeps a long run's context small. A sub-agent can
read twenty files and answer in one paragraph, and only the paragraph reaches the caller. What it spent counts all
the same: the `Usage` of the caller's response adds the tokens of the sub-agent's model calls to the caller's own,
those of the sub-agent's own sub-agents too, and the caller's stored entries hold them
([What is stored](./conversations.md#what-is-stored)). A background sub-agent's tokens count once it answered. When the calling agent's run
options set `WithConcurrentToolCalls(true)`, the sub-agents the model calls in one response work at the same time, in
the caller's scope ([Tool calls at the same time](./defining-agents.md#tool-calls-at-the-same-time)).

Run the calling agent like any other:

<!-- snippet: agent-as-tool-run -->
<a id='snippet-agent-as-tool-run'></a>
```cs
var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>()
    .WithSettingsId(settingsId);

var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MultiAgent.cs#L15-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A sub-agent needs a description and a lower_snake_case name ([TSH023](./diagnostics.md#tsh023)), because its name
becomes the tool's and the model reads its description to decide when to call it. That name is the agent's alone:
no tool of the calling agent and no other sub-agent of it may have it ([TSH025](./diagnostics.md#tsh025)). The
analyzer rejects agents that call each other in a circle through `UseAgent` ([TSH026](./diagnostics.md#tsh026)),
but handoffs may form circles.

::: warning A sub-agent cannot ask for approval
A sub-agent cannot stop to ask your app for approval. In the default mode, each of its calls that needs approval is
rejected unless a standing approval or a rule covers it, and the sub-agent's model reads why. The analyzer warns
about such tools
([TSH024](./diagnostics.md#tsh024)), so add a rule, or hand off to the agent instead.
:::

A sub-agent cannot [ask the user](./tool-approval.md#asking-the-user) either. An agent that declares
`UseQuestions()` is not offered `ask_user` while it runs as a sub-agent, and the analyzer warns at its `UseAgent<T>()`
([TSH034](./diagnostics.md#tsh034)). Declare `UseQuestions()` on the calling agent, which asks on the sub-agent's
behalf, or hand off to the agent.

### Typed answers

By default, the calling model reads the sub-agent's answer as text. `WithResult<T>()` asks the sub-agent for JSON of
`T`'s schema instead, as [`RunAsync<T>`](./running-agents.md#structured-output) does, and hands that JSON to the
calling model as the call's result:

<!-- snippet: agent-as-tool-result -->
<a id='snippet-agent-as-tool-result'></a>
```cs
internal partial class MarketAgent : Agent
{
    public MarketAgent()
    {
        WithInstructions(
            "Compare the markets of the cities you are given. Ask research_agent for each city's companies.");

        WithChatOptions()
            .WithProvider("anthropic")
            .WithModel("claude-sonnet-5-5");

        UseAgent<ResearchAgent>()
            .WithResult<CityCompanies>();
    }
}

internal sealed record CityCompanies(string City, IReadOnlyList<string> Companies);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/MarketAgent.cs#L5-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool-result' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

- The calling model gets an object it can read field by field, here a city and its companies, instead of prose it
  has to pick apart.
- When the sub-agent answers without that JSON, the call fails, and the calling model reads why.
- In the background, the JSON arrives as the sub-agent's answer, and so does the failure.
- Under [Native AOT](./native-aot.md), `T` needs JSON metadata, as for `RunAsync<T>`. Without it, the call
  fails. The calling model reads only that it failed, and the `NotSupportedException` that names the type is in
  your log and on the call's span.

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
A run takes no other kind of message between two model calls, so it is in the user's role. Its `AuthorName` names
the sub-agent, and its first line tells the model that what follows is that agent's output and not a message from
the user, since the answer may repeat what the sub-agent read, a web page for example. The conversation stores it
too, and `message.IsBackgroundAnswer` tells it apart from what the user wrote, so a chat view can show it as the
sub-agent's.

- A run does not end while its sub-agents work. Once the model stops calling tools, the run waits for the next
  answer and continues with it.
- A run that ends some other way, for example with an error or a cancellation, stops the sub-agents still working
  and waits until they have stopped, so none of them works on after the run. A sub-agent stops once its model call
  or tool call sees the cancellation, so a tool that ignores its `CancellationToken` holds up the end of the run.
- A [handoff](#handoff) stops them the same way: the sub-agents the handing agent left working stop before the
  other agent takes over, and that agent reads which of them were lost.
- When a background sub-agent never answered, because the calling run ended first or the process crashed, the
  conversation's next run tells the model it was lost, so the model can call it again.
- A background sub-agent that fails tells the calling model so in its message, without the exception's message.
  Typeshift logs the exception as an error and records it on the calling run's span.
- It works in the caller's scope, beside the caller's next tool calls, so its tools and the caller's share that
  scope's services ([Tool calls at the same time](./defining-agents.md#tool-calls-at-the-same-time)).

The sub-agent itself is an agent like any other. This one explores the workspace with a fast model and offers only
the reading tools of the app's `WorkspaceToolset`, so none of its calls needs approval:

<!-- snippet: sub-agent -->
<a id='snippet-sub-agent'></a>
```cs
internal partial class ExploreAgent : Agent
{
    public ExploreAgent()
    {
        WithDescription("Explores the workspace and answers one question about it, citing files and lines.");

        WithInstructions("Answer the question from the files. Read only what you need; quote the lines you rely on.");

        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini");

        UseToolset<WorkspaceToolset>()
            .Include(t => t.ReadFile)
            .Include(t => t.ListFiles)
            .Include(t => t.SearchFiles);
    }
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Agents/ExploreAgent.cs#L5-L24' title='Snippet source file'>snippet source</a> | <a href='#snippet-sub-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Typeshift ships no file or shell tools, so `WorkspaceToolset` is yours to write. The
[Coder demo](https://github.com/weboost-at/Typeshift/tree/main/demos/Typeshift.Demos.Coder) has complete ones, and
an explorer like this one that works in the background.

## Handoff

`UseHandoff<T>()` gives the model a tool, `handoff_to_{name}`, that carries the other agent's description. For
`BillingAgent` it is `handoff_to_billing_agent`. When the model calls
it, the other agent takes over and answers in the same run, with the whole conversation and its own instructions,
tools and stored settings. Like a sub-agent, it needs a description and a lower_snake_case name
([TSH023](./diagnostics.md#tsh023)). Names that start with `handoff_to_` belong to these tools, so no tool of a
toolset or an agent may have one ([TSH035](./diagnostics.md#tsh035)):

<!-- snippet: handoff -->
<a id='snippet-handoff'></a>
```cs
internal partial class TriageAgent : Agent
{
    public TriageAgent()
    {
        WithInstructions("Find out what the customer needs, then hand off to support or billing.");

        WithChatOptions()
            .WithProvider("openai")
            .WithModel("gpt-5-mini");

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
var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>()
    .WithSettingsId(settingsId)
    .WithConversationId(21);

var first = await triage.RunAsync("I was charged twice.");
var second = await triage.RunAsync("It was invoice 2024-17.");

Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/MultiAgent.cs#L24-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-handoff-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A handoff ends the handing agent's turn, and none of the tool calls the model made in the same response runs.
Each gets a result that says it did not run because the conversation was handed off, so the agent that takes over
reads what was left undone and can do it. When one response calls two handoffs, the last one is carried out. A
handoff called next to a call that [waits for your app](./tool-approval.md#when-a-call-needs-approval) is not carried
out: the model reads that it was not handed off, and the agent that asked keeps the conversation.

[Plan mode](./tool-approval.md#plan-mode) offers no handoff tools, so an agent hands off only in the default or the
auto mode.

A run hands off at most ten times. Each agent that takes the run over counts its own
[rounds of tool calls](./builders.md#runoptionsbuilder), so without that limit two agents that each send the user to the
other would call their models for as long as your app waits. The eleventh handoff call is answered with a note that
the run has no handoffs left, and the agent that made it answers the user itself.

The `Settings` you pass to one run apply only to the agent you started it on. An agent it hands off to runs with its
own settings stored under the same settings id. The mode is the one exception: a `Mode` in a run's settings switches
the conversation, so the agent that answers the run is in it too, as it is in the mode of an approved plan
([Setting the mode](./tool-approval.md#setting-the-mode)). A provider the call site names serves the agent handed to
as well, with that agent's own model, and so does it serve the agent that answers a later run of the conversation. A
run checks the providers of the agents it may hand off to when it starts, as it checks its sub-agents'.

::: warning Handoffs need a conversation
A run without a conversation id does not offer the handoff tools, so its agent cannot hand off, unless your app
[keeps every conversation](./conversations.md#keeping-every-conversation).
:::

## Related

- [Agents](./defining-agents.md#declaring-an-agent): declaring the other agents an agent uses.
- [Settings layering](./settings-and-stores.md#settings-layering): which settings sub-agents and handoffs run with.
- [Approval rules](./tool-approval.md#approval-rules): rules that decide a sub-agent's calls.
- [Compaction](./conversations.md#compaction): sub-agents that keep a long run's context small.
- [Conversations](./conversations.md): where a handoff's conversation is stored.
