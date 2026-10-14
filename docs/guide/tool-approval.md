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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Tickets/TicketToolset.cs#L24-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-tool' title='Start of snippet'>anchor</a></sup>
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
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L16-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-run' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Calls in the same response that need no approval run once the host answered. A run that asks needs a
[conversation](./conversations.md) to store the request in: one without a conversation id throws, naming the tool.

## Approving or rejecting

The next run of the conversation answers each request. Approved calls run, rejected ones reach the model as
rejected, with the reason if the answer gives one, and the run goes on:

<!-- snippet: approval-resume -->
<a id='snippet-approval-resume'></a>
```cs
var approvals = answer.ApprovalRequests.Select(request => request.CreateResponse(approved: true));

var approval = new ChatMessage(ChatRole.User, [.. approvals]);

var resumed = await support.RunAsync(approval, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L24-L30' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-resume' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Each answer is matched to the request the conversation stored, so the call that runs is the one the host saw,
arguments included, whatever call the answer carries. An answer to no stored request is dropped, and a request keeps
its first answer. A request left unanswered is dropped from the next run, so the model sees that call as never made.

## Approving for the conversation

`ApproveForConversation()` approves the call and every later call of the same tool in the conversation. The approval
is stored with the conversation, so it holds across runs and processes:

<!-- snippet: approval-for-conversation -->
<a id='snippet-approval-for-conversation'></a>
```cs
var always = new ChatMessage(ChatRole.User, [again.ApprovalRequests[0].ApproveForConversation()]);

await support.RunAsync(always, conversationId: conversationId);
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Approval.cs#L37-L41' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-for-conversation' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Approval rules

A rule decides calls before the host is asked: it approves what the host trusts, rejects what it never allows, and
abstains on the rest. Register it with `WithApprovalRule<T>()`; it is resolved from the run's services:

<!-- snippet: approval-rule-registration -->
<a id='snippet-approval-rule-registration'></a>
```cs
services.AddTypeshift()
    .WithApprovalRule<ShellGuard>();
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Scenarios/Registration.cs#L56-L59' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-rule-registration' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Every rule sees every call that requires approval. A rejection wins over every approval, and the model receives its
reason:

<!-- snippet: approval-rule -->
<a id='snippet-approval-rule'></a>
```cs
internal sealed class ShellGuard : IToolApprovalRule
{
    private static readonly string[] Safe = ["dotnet build", "dotnet test", "git status", "git diff"];

    private static readonly string[] Never = ["sudo", "rm -rf", "curl", "ssh"];

    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(call is { Name: "run_command", Arguments: { } arguments }
            && arguments["command"] is string command
                ? Decide(command)
                : ToolDecision.Abstain);

    private static ToolDecision Decide(string command) =>
        command switch
        {
            _ when Never.Any(command.Contains) => ToolDecision.Reject("This command is never allowed here."),
            _ when command.IndexOfAny([';', '&', '|', '`', '$', '>', '<', '\n']) >= 0 => ToolDecision.Abstain,
            _ when Safe.Any(command.StartsWith) => ToolDecision.Approve,
            _ => ToolDecision.Abstain
        };
}
```
<sup><a href='https://github.com/weboost-at/Typeshift/blob/main/samples/Typeshift.Samples.Docs/Hosting/ShellGuard.cs#L5-L27' title='Snippet source file'>snippet source</a> | <a href='#snippet-approval-rule' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

A rule sees the arguments the model sent, so it decides on exactly the call that runs. Abstain on what you cannot
read: the rule above leaves every command that chains, pipes or redirects to the host. What an abstention means
depends on the [mode](./modes.md): the host is asked in the default mode, the call runs in auto mode.
