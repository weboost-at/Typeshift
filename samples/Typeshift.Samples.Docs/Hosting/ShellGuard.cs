using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: approval-rule
internal sealed class ShellGuard : IToolApprovalRule
{
    private static readonly string[] Never = ["sudo", "rm -rf", "curl", "wget", "ssh"];

    // A provider sends each argument as a JsonElement, whose ToString() is the string itself
    public ValueTask<RuleVerdict> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct) =>
        ValueTask.FromResult(call is { Name: "run_command", Arguments: { } arguments }
            && arguments.TryGetValue("command", out var value)
            && value?.ToString() is { } command
            && Never.Any(command.Contains)
                ? RuleVerdict.Reject("This command is never allowed here.")
                : RuleVerdict.Abstain);
}
// end-snippet
