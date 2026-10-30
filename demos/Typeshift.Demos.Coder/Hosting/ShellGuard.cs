using Microsoft.Extensions.AI;

namespace Typeshift.Demos.Coder.Hosting;

// Rejects the commands never allowed here, approves the ones that only look, and leaves the rest to the host (or to
// auto mode once the plan is approved)
public sealed class ShellGuard : IToolApprovalRule
{
    private static readonly string[] Never = ["sudo", "rm -rf", "curl", "wget", "ssh", "mkfs", "> /dev/"];

    private static readonly string[] Looks = ["git status", "git diff", "git log", "ls", "cat ", "wc "];

    // A command that chains, pipes or substitutes another runs more than its start says, so it is never approved here
    private static readonly char[] Chains = [';', '&', '|', '\n', '`', '$', '>', '<'];

    public ValueTask<ToolDecision> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct)
    {
        if (call is not { Name: "run_command", Arguments: { } arguments }
            || !arguments.TryGetValue("command", out var sent)
            || sent?.ToString() is not { } command)
            return ValueTask.FromResult(ToolDecision.Abstain);

        var decision = Never.Any(never => command.Contains(never, StringComparison.Ordinal))
            ? ToolDecision.Reject($"'{command}' is never allowed in this workspace.")
            : Looks.Any(looks => command.StartsWith(looks, StringComparison.Ordinal)) && command.IndexOfAny(Chains) < 0
                ? ToolDecision.Approve
                : ToolDecision.Abstain;

        return ValueTask.FromResult(decision);
    }
}
