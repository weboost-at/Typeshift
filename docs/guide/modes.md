---
title: Plan and auto modes
description: Decide what a run offers the model and whether tools that require approval ask, with the default, plan and auto modes.
---

# Plan and auto modes

Every run is in one of three modes. The mode decides which tools the model is offered, and what happens to tools that
[require approval](./tool-approval.md). Plan mode lets the model research and propose a plan before it changes anything.
Auto mode lets it work without asking, guarded by your approval rules.

::: tip The short version

- `Default` asks the host, `Plan` researches and proposes a plan, `Auto` runs without asking.
- The agent's run options set the mode runs start in. `Configurable(r => r.Mode)` lets settings change it.
- A conversation keeps its mode until a run's settings name another.
- In plan mode, `exit_plan_mode` presents the plan. `ApprovePlan(mode)` continues in `Default` or `Auto`.
- Auto mode is as safe as your approval rules: a rule that rejects a call wins.

:::

## The three modes

| Mode | Offers | Tools that require approval |
|---|---|---|
| `Default` | every tool | ask the host, unless a rule or a standing approval decides |
| `Plan` | read-only tools and `exit_plan_mode` | offered only if also `ReadOnly()`; they still ask |
| `Auto` | every tool | run without asking, unless a rule rejects the call |

## Setting the mode

The mode is a [run option](./defining-agents.md#run-options). The agent's code sets the mode runs start in, `Default`
unless set. `Configurable(r => r.Mode)` lets stored settings or a run change it:

<!-- snippet: switch-mode -->
<a id='snippet-switch-mode'></a>
```cs
var plan = new TriageAgent.Settings { Mode = AgentMode.Plan };

var planned = await triage.RunAsync("Sort out ticket 1234.", settingsId, conversationId, plan);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-switch-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A conversation keeps its mode. A run that sets no mode of its own continues in the mode the conversation's last run
ended in. A run whose settings name a mode switches the conversation to it.

::: warning A conversation stays in the mode it ended in
After the host approves a plan with `ApprovePlan(AgentMode.Auto)`, later runs of the conversation run in auto mode,
whatever mode the agent's code starts runs in. Pass a mode in a run's settings to switch back.
:::

## Plan mode

In plan mode the model sees only the tools marked `ReadOnly()`, a toolset's or an [MCP server's](./mcp.md), and
`exit_plan_mode`. Typeshift adds a short instruction: research, then present the plan. So the model changes nothing
until the host has approved the plan.

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

Approve it with `ApprovePlan(mode)` to continue the work in the mode the host chooses, `Default` or `Auto`. Reject it
with a reason, and the model goes back to planning with that feedback:

<!-- snippet: approve-plan -->
<a id='snippet-approve-plan'></a>
```cs
var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

await triage.RunAsync(approval, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L29-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-approve-plan' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

::: warning Plan mode hides every tool without the mark
A tool without `ReadOnly()` is hidden in plan mode, whatever it does. Mark each tool that only reads, so the model can
research with it.
:::

## Auto mode

In auto mode, tools that require approval run without asking. Rules still see every such call first, and a rule that
rejects one wins.

::: danger Auto mode is as safe as your rules
Without a rule that rejects it, every call that requires approval runs in auto mode. Guard it with [approval
rules](./tool-approval.md#approval-rules): for example, a guard that never lets `sudo` through, or a classifier that
asks a model.
:::

## Related

- [Tool approval](./tool-approval.md): requests, standing approvals and rules.
- [Agents](./defining-agents.md#run-options): the run options that set the mode runs start in.
- [Long-running agents](./conversations.md#compaction): plan and auto mode in a run that works on its own.
- [MCP servers](./mcp.md): marking a server's tools read-only, so plan mode offers them.
