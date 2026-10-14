# Tool approval

A tool that must not run unsupervised, closing a ticket or refunding an invoice, asks the host first. Typeshift uses
Microsoft.Extensions.AI's approval contents, `ToolApprovalRequestContent` and `ToolApprovalResponseContent`.

## Requiring approval

<!-- snippet: approval-tool -->
<a id='snippet-approval-tool'></a>
```cs
AddTool(CloseTicketAsync)
    .RequiresApproval();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Tickets/TicketToolset.cs#L22-L25' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-tool' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## A run that asks

When the model calls such a tool, the run ends with the call unmade. The response lists what waits for approval:

<!-- snippet: approval-run -->
<a id='snippet-approval-run'></a>
```cs
var answer = await support.RunAsync("Close ticket 1234.", conversationId: conversationId);

foreach (var request in answer.ApprovalRequests)
    if (request.ToolCall is FunctionCallContent call)
        Console.WriteLine($"Approve {call.Name}({string.Join(", ", call.Arguments!.Values)})?");
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Approval.cs#L15-L21' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Calls in the same response that need no approval run as usual. A run that asks needs a conversation to resume: one
without a conversation id throws, naming the tool.

## Approving or rejecting

The next run of the conversation answers each request. Approved calls run, rejected ones reach the model as
rejected, and the run goes on:

<!-- snippet: approval-resume -->
<a id='snippet-approval-resume'></a>
```cs
var approvals = answer.ApprovalRequests.Select(request => request.CreateResponse(approved: true));

var approval = new ChatMessage(ChatRole.User, [.. approvals]);

var resumed = await support.RunAsync(approval, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Approval.cs#L23-L29' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each response is matched to the request the conversation stored, so the call that runs is the one the host saw,
arguments included.

## Approving for the conversation

`ApproveForConversation()` approves the call and every later call of the same tool in the conversation, across runs
and processes, since the approval is stored with the conversation:

<!-- snippet: approval-for-conversation -->
<a id='snippet-approval-for-conversation'></a>
```cs
var always = new ChatMessage(ChatRole.User, [answer.ApprovalRequests[0].ApproveForConversation()]);

await support.RunAsync(always, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Scenarios/Approval.cs#L31-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-for-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Approval rules

A rule approves calls the host trusts without asking. Register it with `WithApprovalRule<T>()`; it is resolved from
the run's services, and any rule that approves suffices:

<!-- snippet: approval-rule -->
<a id='snippet-approval-rule'></a>
```cs
internal sealed class SafeCommands : IToolApprovalRule
{
    private static readonly string[] Allowed = ["dotnet build", "dotnet test", "git status", "git diff"];

    public ValueTask<bool> ApprovesAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(
            call is { Name: "run_command", Arguments: { } arguments }
            && arguments["command"] is string command
            && command.IndexOfAny([';', '&', '|', '`', '$', '>', '<', '\n']) < 0
            && Allowed.Any(command.StartsWith));
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/claude/rewrite/signoff/Typeshift.Samples/Hosting/SafeCommands.cs#L5-L17' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-rule' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A rule sees the arguments the model sent, so it decides on exactly the call that runs. Refuse what you cannot read:
the rule above rejects every command that chains, pipes or substitutes.

## MCP servers

`UseMcpServer(...).RequiresApproval()` asks before every tool of the server; see [MCP](./mcp.md).
