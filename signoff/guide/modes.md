# Plan and auto modes

Every run is in one of three modes, which decide what happens to tools the agent may not run unasked:

| Mode | Offers | Tools that require approval |
|---|---|---|
| `Default` | every tool | ask the host, unless a standing approval or a rule decides |
| `Plan` | read-only tools and `exit_plan_mode` | not offered |
| `Auto` | every tool | run without asking, unless a rule rejects the call |

## Setting the mode

The mode is a run option: the agent's code sets where runs start, and `Configurable` lets stored settings or a run
change it.

<!-- snippet: long-running-options -->
<a id='snippet-long-running-options'></a>
```cs
WithRunOptions()
    .WithMode(AgentMode.Plan)
    .WithMaxToolIterations(500)
    .WithMaxInputTokens(150_000)
    .WithSummaryModel("fast-model")
    .Configurable(r => r.Mode)
    .Configurable(r => r.MaxToolIterations);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Agents/CodingAgent.cs#L21-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-long-running-options' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A conversation keeps its mode: a run without a mode of its own continues in the mode the conversation's last run
ended in. A run's settings switch it:

<!-- snippet: switch-mode -->
<a id='snippet-switch-mode'></a>
```cs
var plan = new CodingAgent.Settings { Mode = AgentMode.Plan };

await coding.RunAsync("Now plan the move to central package management.", settingsId, conversationId, plan);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Modes.cs#L28-L32' title='Snippet source file'>snippet source</a> | <a href='#snippet-switch-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Plan mode

In plan mode the model sees only tools marked `ReadOnly()`, `write_todos`, and `exit_plan_mode`. Typeshift adds a
short instruction: research, then present the plan. `exit_plan_mode(plan)` ends the run with an approval request
that carries the plan:

<!-- snippet: plan-mode -->
<a id='snippet-plan-mode'></a>
```cs
var planned = await coding.RunAsync("Move the solution to .NET 10.", settingsId, conversationId);

var proposal = planned.ApprovalRequests.Single();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Modes.cs#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-plan-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Approving it continues the work in the mode the host chooses; rejecting it with a reason sends the model back to
planning with that feedback:

<!-- snippet: approve-plan -->
<a id='snippet-approve-plan'></a>
```cs
var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

await coding.RunAsync(approval, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Modes.cs#L22-L26' title='Snippet source file'>snippet source</a> | <a href='#snippet-approve-plan' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Mark tools that only read with `ReadOnly()` (the built-in file, web and todo tools are), and MCP servers with
`UseMcpServer(...).ReadOnly()`. A tool without the mark is hidden in plan mode, whatever it does.

## Auto mode

In auto mode, tools that require approval run without asking. Rules still see every such call first, and a rule
that rejects one wins, so auto mode is as safe as the host's rules make it: a guard that never lets `sudo` through,
say, or a classifier that asks a model. See [approval rules](./tool-approval.md#approval-rules).

## Sub-agents

A [sub-agent](./multi-agent.md#sub-agents) runs in the mode of the run that called it, so a sub-agent of a planning
run only reads.
