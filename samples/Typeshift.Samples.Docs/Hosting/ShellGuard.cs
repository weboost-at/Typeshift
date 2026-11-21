using Microsoft.Extensions.AI;
using Typeshift.Samples.Workspace;

namespace Typeshift.Samples.Hosting;

// begin-snippet: approval-rule
internal sealed class ShellGuard : IToolApprovalRule
{
    // Whole commands: one that only starts with or contains one of them is another command
    private static readonly string[] Allowed = ["dotnet build", "dotnet test", "git status", "git diff"];

    // In auto mode nobody is asked, so a command the rule cannot name is rejected; elsewhere your app decides.
    // A provider sends each argument as a JsonElement, whose ToString() is the string itself
    public ValueTask<RuleVerdict> DecideAsync(
        FunctionCallContent call,
        AgentRun run,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(call is { Name: WorkspaceToolset.ToolNames.RunCommand, Arguments: { } arguments }
            && run.Mode == AgentMode.Auto
            && !(arguments.TryGetValue("command", out var value)
                && value?.ToString() is { } command
                && Allowed.Contains(command))
                ? RuleVerdict.Reject($"In auto mode only these commands run: {string.Join(", ", Allowed)}.")
                : RuleVerdict.Abstain);
}
// end-snippet
