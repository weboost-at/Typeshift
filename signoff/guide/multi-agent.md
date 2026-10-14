# Multiple agents

An agent can call another agent as a sub-agent, or hand the conversation to it.

## Sub-agents

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

A call runs the sub-agent in a fresh context: the same settings id, so its own settings stored there apply, the
caller's mode, standing approvals and rules, and no conversation of its own. It returns its answer's text. Its tool
calls and tokens stay out of the caller's history, which is what keeps a long run's context small: a sub-agent reads
twenty files and answers in a paragraph. With `WithConcurrentToolCalls(true)`, the sub-agents of one model response
work at the same time.

<!-- snippet: agent-as-tool-run -->
<a id='snippet-agent-as-tool-run'></a>
```cs
var reports = scope.ServiceProvider.GetRequiredService<AgentHost<ReportAgent>>();

var report = await reports.RunAsync("Compare the robotics scenes of Vienna and Graz.", settingsId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/MultiAgent.cs#L13-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-agent-as-tool-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A sub-agent cannot ask the host: in the default mode, a call of its that needs approval and that no standing approval
or rule covers is rejected, and the analyzer warns about such tools ([TSH024](./diagnostics.md#tsh024)). A sub-agent
needs a description ([TSH023](./diagnostics.md#tsh023)); agents that call each other in a circle are
[TSH026](./diagnostics.md#tsh026).

### In the background

`InBackground()` starts the sub-agent and returns at once, so the caller goes on working:

<!-- snippet: background-sub-agent -->
<a id='snippet-background-sub-agent'></a>
```cs
UseAgent<ExploreAgent>().InBackground();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/CodingAgent.cs#L17-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-background-sub-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

When the sub-agent answers, its answer reaches the calling run as a message before the run's next model call. A run
does not end while its sub-agents work: once the model stops calling tools, the run waits for the next answer and
continues with it. A sub-agent lost to a crash is reported to the model as lost when the run resumes.

<!-- snippet: sub-agent -->
<a id='snippet-sub-agent'></a>
```cs
internal partial class ExploreAgent : Agent
{
    public ExploreAgent()
    {
        WithDescription("Explores the workspace and answers one question about it, citing files and lines.");

        WithChatOptions().WithModel("fast-model");

        UseToolset<FileSystemToolset>().Include(t => t.ReadFile).Include(t => t.ListFiles).Include(t => t.SearchFiles);
    }

    protected override string GetInstructions() =>
        "Answer the question from the files. Read only what you need; quote the lines you rely on.";
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/ExploreAgent.cs#L5-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-sub-agent' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

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
