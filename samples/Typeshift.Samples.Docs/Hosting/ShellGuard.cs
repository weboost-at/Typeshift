using Microsoft.Extensions.AI;

namespace Typeshift.Samples.Hosting;

// begin-snippet: approval-rule
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
// end-snippet
