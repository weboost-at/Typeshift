using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: approval-rule
internal sealed class ShellGuard : IToolApprovalRule
{
    private static readonly string[] Never = ["sudo", "rm -rf", "curl", "wget", "ssh"];

    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(call is { Name: "run_command", Arguments: { } arguments }
            && arguments["command"] is string command
            && Never.Any(command.Contains)
                ? ToolDecision.Reject("This command is never allowed here.")
                : ToolDecision.Abstain);
}
// end-snippet
