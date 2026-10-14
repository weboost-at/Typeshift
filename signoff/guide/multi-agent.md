# Multiple agents

An agent can call another agent as a tool, or hand the conversation to it.

## An agent as a tool

`UseAgent<T>()` offers another agent as a tool named like it, described by its `WithDescription`, with one `input`
parameter:

<!-- snippet: agent-as-tool -->
<a id='snippet-agent-as-tool'></a>
```cs
internal partial class ReportAgent : Agent
{
    public ReportAgent()
    {
        WithChatOptions().WithModel("reasoning-model");

        UseAgent<ResearchAgent>();
    }

    protected override string GetInstructions() =>
        "Write a market report. Ask research_agent for the companies of each city you cover.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/ReportAgent.cs#L5-L18' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A call runs the other agent with the same settings id, so its own settings stored there apply, and no conversation;
it returns its answer's text. It is a run of its own, so its tool calls and tokens stay out of the caller's history.

<!-- snippet: agent-as-tool-run -->
<a id='snippet-agent-as-tool-run'></a>
```cs
var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/MultiAgent.cs#L13-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

An agent used as a tool needs a description ([TSH023](./diagnostics.md#tsh023)) and may not offer tools that need
approval ([TSH024](./diagnostics.md#tsh024)): nobody would be asked. Agents that call each other in a circle are
[TSH026](./diagnostics.md#tsh026).

## Handoff

`UseHandoff<T>()` gives the model a tool `handoff_to_{name}`, described like the other agent. When the model calls
it, the other agent takes over: it answers in the same run, with the whole conversation, its own instructions, tools
and stored settings.

<!-- snippet: handoff -->
<a id='snippet-handoff'></a>
```cs
internal partial class TriageAgent : Agent
{
    public TriageAgent()
    {
        WithChatOptions().WithModel("fast-model");

        UseToolset<TicketToolset>().Exclude(t => t.CloseTicket);

        UseHandoff<SupportAgent>();
        UseHandoff<BillingAgent>();
    }

    protected override string GetInstructions() =>
        "Find out what the customer needs, then hand off to support or billing.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/TriageAgent.cs#L5-L23' title='Snippet source file'>snippet source</a> | <a href='#snippet-handoff' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

The conversation remembers who answered last, so its later runs go to that agent, whichever host runs them; each
message carries its agent's name as `AuthorName`:

<!-- snippet: handoff-run -->
<a id='snippet-handoff-run'></a>
```cs
var triage = scope.ServiceProvider.GetRequiredService<AgentHost<TriageAgent>>();
var conversationId = 8;

var first = await triage.RunAsync("I was charged twice.", settingsId, conversationId);
var second = await triage.RunAsync("It was invoice 2024-17.", settingsId, conversationId);

Console.WriteLine($"{second.Messages[^1].AuthorName}: {second.Text}");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/MultiAgent.cs#L19-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-handoff-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Handoffs need a conversation. A run's own `Settings` apply to the agent it was started on; agents it hands off to run
with their settings stored under the same settings id.
