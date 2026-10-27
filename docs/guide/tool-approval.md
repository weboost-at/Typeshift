---
title: Approval and modes
description: Pause a run until your app approves a tool call, let rules decide the calls you are sure about, and pick the default, plan or auto mode.
---

# Approval and modes

Some tools must not run unasked, such as closing a ticket. When the model calls one, the run ends and hands the call
to your app, which approves or rejects it in the conversation's next run. Approval rules, standing approvals and the
run's mode decide which calls need asking at all.

## The round trip

```mermaid
sequenceDiagram
  participant App as Your code
  participant Host as AgentHost
  participant Model
  participant Tool as close_ticket
  App->>Host: RunAsync("Close ticket 1234.")
  Host->>Model: The conversation and the input
  Model-->>Host: Call close_ticket
  Note over Host: No rule or standing approval decides,<br/>so the run stores the request and ends
  Host-->>App: The response, with ApprovalRequests
  App->>Host: RunAsync(the answers)
  Host->>Tool: The call the conversation stored
  Host->>Model: The tool's result
  Model-->>Host: The answer
  Host-->>App: The response
```

Because no [approval rule](#approval-rules) or [standing approval](#approving-for-the-conversation) decides the call
to `close_ticket`, the run stores the request in the conversation and ends. Your app sends its answer in the
conversation's next run, and the stored call runs once that answer approves it. Requests and answers are
Microsoft.Extensions.AI's `ToolApprovalRequestContent` and `ToolApprovalResponseContent`.

## Requiring approval

`RequiresApproval()` makes every call to a tool wait for approval:

<!-- snippet: approval-tool -->
<a id='snippet-approval-tool'></a>
```cs
AddTool(CloseTicketAsync)
    .RequiresApproval();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L24-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

`UseMcpServer(...).RequiresApproval()` does the same for every tool of an [MCP server](./mcp.md). A tool that a
[context provider](./middleware.md#context-providers) adds needs approval when you wrap it in
`ApprovalRequiredAIFunction`.

## A run that asks

When the model calls such a tool, the run ends without making the call. `ApprovalRequests` on the response lists each
waiting call, with the tool's name and the arguments the model sent:

<!-- snippet: approval-run -->
<a id='snippet-approval-run'></a>
```cs
var answer = await support.RunAsync("Close ticket 1234.", conversationId: conversationId);

foreach (var request in answer.ApprovalRequests)
    if (request.ToolCall is FunctionCallContent call)
        Console.WriteLine($"Approve {call.Name}({string.Join(", ", call.Arguments!.Values)})?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

If the model called other tools in the same response, those calls wait too, even when they need no approval, and run
once your app has answered.

A run that asks needs a [conversation](./conversations.md) to store the request in. A run without a conversation id
throws, naming the tool, so run with one, approve the calls with a rule, or use [auto mode](#auto-mode).

## Approving or rejecting

Answer each request in the conversation's next run with `CreateResponse(approved)`:

<!-- snippet: approval-resume -->
<a id='snippet-approval-resume'></a>
```cs
var approvals = answer.ApprovalRequests.Select(request => request.CreateResponse(approved: true));

var approval = new ChatMessage(ChatRole.User, [.. approvals]);

var resumed = await support.RunAsync(approval, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L24-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Approved calls run. A rejected call reaches the model as rejected, and the run goes on. To tell the model why, pass a
reason with the rejection: `CreateResponse(approved: false, reason)`.

`WithMaxToolIterations` limits the rounds of tool calls of one run. The run that asked has ended, so the calls you
approve run in the next run, which starts its own count: they run even when the run that asked had used up its
rounds. Calls that a rule, a standing approval or auto mode approves go on in the same run and count toward its
limit, and such a run may make one round more than the limit.

Typeshift matches each answer to the request the conversation stored and runs the call stored with it. An answer can
carry a call of its own, but the call that runs is always the one your app saw, with the same arguments. An answer
that matches no stored request is dropped, and when a request gets more than one answer, the first one counts.

::: warning A request left unanswered is dropped
The next run drops every stored request it has no answer for, and the model sees that call as never made. Answer
every request in `ApprovalRequests` in the same run.
:::

## Approving for the conversation

`ApproveForConversation()` approves the call and every later call to the same tool in the conversation. It is stored
with the conversation, so it holds across runs and processes:

<!-- snippet: approval-for-conversation -->
<a id='snippet-approval-for-conversation'></a>
```cs
var again = await support.RunAsync("Close ticket 1234 again.", conversationId: conversationId);

var always = new ChatMessage(ChatRole.User, [again.ApprovalRequests[0].ApproveForConversation()]);

await support.RunAsync(always, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L35-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-for-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Approval rules

A rule decides a call before your app is asked. It approves what you trust, rejects what you never allow, and
abstains on the rest. Register it with `WithApprovalRule<T>()`, and Typeshift resolves it from the run's services:

<!-- snippet: approval-rule-registration -->
<a id='snippet-approval-rule-registration'></a>
```cs
services.AddTypeshift()
    .WithApprovalRule<ShellGuard>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L62-L65' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-rule-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Every rule sees every call that requires approval, with the arguments the model sent, so it decides on exactly the
call that runs. This one rejects the shell commands it never allows:

<!-- snippet: approval-rule -->
<a id='snippet-approval-rule'></a>
```cs
internal sealed class ShellGuard : IToolApprovalRule
{
    private static readonly string[] Never = ["sudo", "rm -rf", "curl", "wget", "ssh"];

    // A provider sends each argument as a JsonElement, whose ToString() is the string itself
    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(call is { Name: "run_command", Arguments: { } arguments }
            && arguments.TryGetValue("command", out var value)
            && value?.ToString() is { } command
            && Never.Any(command.Contains)
                ? ToolDecision.Reject("This command is never allowed here.")
                : ToolDecision.Abstain);
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/ShellGuard.cs#L5-L19' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-rule' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

| Decision | Means |
|---|---|
| `ToolDecision.Reject(reason)` | The call is rejected in every mode, and the model receives the reason. A rejection wins over every approval. |
| `ToolDecision.Approve` | The call runs, unless another rule rejects it. |
| `ToolDecision.Abstain` | No opinion. Another rule, a standing approval or the [mode](#the-three-modes) decides: auto mode runs the call, and the default and plan modes ask your app. |

::: danger Approve only what you can read in full
A command that looks harmless may run code the model wrote. That is why the rule above only rejects and leaves the
rest to your app. [The shell](./built-in-toolsets.md#shell) has the details.
:::

## The three modes

Every run is in one of three modes. The mode decides which tools the model is offered and what happens to the tools
that require approval:

| Mode | Offers | Tools that require approval |
|---|---|---|
| `Default` | every tool | ask your app, unless a rule or a standing approval decides |
| `Plan` | read-only tools and `exit_plan_mode` | offered only if also marked `ReadOnly()`, and then ask as in `Default` |
| `Auto` | every tool | run without asking, unless a rule rejects the call |

## Setting the mode

The mode is a [run option](./defining-agents.md#run-options). `WithMode(mode)` in the agent's `WithRunOptions()` sets
the mode runs start in, `Default` unless set. Add `Configurable(r => r.Mode)` to the same chain, and the agent's
generated `Settings` gets a `Mode`, so stored settings or a run can change it. `TriageAgent` does this, so this run
starts in plan mode:

<!-- snippet: switch-mode -->
<a id='snippet-switch-mode'></a>
```cs
var plan = new TriageAgent.Settings { Mode = AgentMode.Plan };

var planned = await triage.RunAsync("Sort out ticket 1234.", settingsId, conversationId, plan);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L16-L20' title='Snippet source file'>snippet source</a> | <a href='#snippet-switch-mode' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A conversation keeps its mode. The agent's code and its stored settings decide only the mode of a conversation's first
run. After that, each run continues in the mode the conversation's last run ended in, unless the settings you pass to
the run name a mode, which switches the conversation to it.

::: warning A conversation stays in the mode it ended in
After your app approves a plan with `ApprovePlan(AgentMode.Auto)`, later runs of the conversation run in auto mode,
whatever mode the agent's code starts runs in. To switch back, pass a mode in a run's settings. That needs
`Configurable(r => r.Mode)`, so add it to every agent that can enter plan mode.
:::

## Plan mode

Plan mode lets the model research and propose a plan before it changes anything, so your app approves the whole plan
before any step runs. Approve the plan into auto mode, and its steps run without asking, unless a rule rejects them.
The model sees only the tools marked `ReadOnly()`, a toolset's or an [MCP server's](./mcp.md), plus its
[sub-agents](./multi-agent.md#sub-agents) and `exit_plan_mode`, and Typeshift adds a short instruction to research,
then present the plan. A sub-agent called in plan mode sees only its own read-only tools and answers its caller, which
presents the plan.

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

Approve it with `ApprovePlan(mode)` to continue in the mode your app picks, `Default` or `Auto`:

<!-- snippet: approve-plan -->
<a id='snippet-approve-plan'></a>
```cs
var approval = new ChatMessage(ChatRole.User, [proposal.ApprovePlan(AgentMode.Auto)]);

await triage.RunAsync(approval, settingsId, conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Modes.cs#L29-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-approve-plan' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

To reject the plan, answer with `CreateResponse(approved: false, reason)`. The model receives the reason as feedback
and goes back to planning.

::: warning Plan mode hides every tool without the mark
A tool without `ReadOnly()` is hidden in plan mode, whatever it does. Mark every tool that only reads, so the model can
research with it.
:::

## Auto mode

In auto mode, tools that require approval run without asking your app. Rules still see each such call first, and a
rule that rejects it wins.

::: danger In auto mode, only your rules stop a call
Guard auto mode with [approval rules](#approval-rules), for example a guard that never lets `sudo` through, or a
classifier that asks a model.
:::

## Related

- [Built-in toolsets](./built-in-toolsets.md#shell): the shell, which needs approval for every command.
- [Conversations](./conversations.md): where requests, standing approvals and the mode are stored.
- [Agents](./defining-agents.md#run-options): the run options that set the mode runs start in.
- [MCP servers](./mcp.md): marking a server's tools read-only, so plan mode offers them.
- [How a run works](./running-agents.md#how-a-run-works): where approval sits among a run's steps.
