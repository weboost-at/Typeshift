# Plan and auto modes

Every run is in one of three modes, which decide what the model is offered and what happens to tools that
[require approval](./tool-approval.md). In `Default`, such tools ask the host; in [`Plan`](#plan-mode), the model
only reads and presents a plan; in [`Auto`](#auto-mode), they run unless a rule rejects the call.
[`AgentMode`](./builders.md#agentmode) sums the three up.

## Setting the mode

The mode is a [run option](./defining-agents.md#run-options): the agent's code sets the mode runs start in, `Default` unless
set, and `Configurable(r => r.Mode)` lets stored settings or a run change it:

<!-- snippet: switch-mode -->
<a id='snippet-switch-mode'></a>
```cs
var plan = new TriageAgent.Settings { Mode = AgentMode.Plan };

var planned = await triage.RunAsync("Sort out ticket 1234.", settingsId, conversationId, plan);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-switch-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A conversation keeps its mode: a run that sets no mode of its own continues in the mode the conversation's last run
ended in. A run whose settings name a mode switches the conversation to it.

## Plan mode

In plan mode the model sees only the tools marked `ReadOnly()`, a toolset's or an [MCP server's](./mcp.md), and
`exit_plan_mode`, and Typeshift adds a short instruction: research, then present the plan. A tool without the mark is
hidden in plan mode, whatever it does.
`exit_plan_mode(plan)` ends the run with an approval request that carries the plan:

<!-- snippet: plan-mode -->
<a id='snippet-plan-mode'></a>
```cs
var proposal = planned.ApprovalRequests.Single();

if (proposal.ToolCall is FunctionCallContent { Arguments: { } arguments })
    Console.WriteLine(arguments["plan"]);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L22-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-plan-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Approving it with `ApprovePlan(mode)` continues the work in the mode the host chooses, `Default` or `Auto`;
rejecting it with a reason sends the model back to planning with that feedback:

<!-- snippet: approve-plan -->
<a id='snippet-approve-plan'></a>
```cs
var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

await triage.RunAsync(approval, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L29-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-approve-plan' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Auto mode

In auto mode, tools that require approval run without asking. Rules still see every such call first, and a rule
that rejects one wins, so auto mode is as safe as the host's rules make it: a guard that never lets `sudo` through,
say, or a classifier that asks a model. See [approval rules](./tool-approval.md#approval-rules).
