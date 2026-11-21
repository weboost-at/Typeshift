using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Typeshift.Demos.Coder.Toolsets;

namespace Typeshift.Demos.Coder.Hosting;

// Approves the commands that read and run nothing the workspace holds: pwd, and ls of the workspace or a folder in
// it. Git is not among them, since a repository's config, which the file tools may write, names commands git runs.
// In auto mode nobody is asked and abstaining runs the call, so there it rejects every command but those and the
// sample workspace's tests; in the default mode it leaves the rest to the console
public sealed partial class ShellGuard : IToolApprovalRule
{
    // Whole commands: one that only starts with or contains one of them is another command
    private static readonly string[] Tests = ["sh test.sh", "./test.sh"];

    public ValueTask<RuleVerdict> DecideAsync(FunctionCallContent call, AgentRun run, CancellationToken ct)
    {
        if (call is not { Name: ShellToolset.ToolNames.RunCommand, Arguments: { } arguments }
            || !arguments.TryGetValue("command", out var sent)
            || sent?.ToString() is not { } command)
            return ValueTask.FromResult(RuleVerdict.Abstain);

        var verdict = (Reads(command, run.Services), run.Mode) switch
        {
            (true, _) => RuleVerdict.Approve,
            (false, AgentMode.Auto) when Tests.Contains(command) => RuleVerdict.Approve,
            (false, AgentMode.Auto) => RuleVerdict.Reject(
                $"In auto mode only pwd, ls and {string.Join(" or ", Tests)} run here. Finish with those, or stop "
                + "and say which command you need and why."),
            _ => RuleVerdict.Abstain
        };

        return ValueTask.FromResult(verdict);
    }

    // pwd, ls, or ls of one folder named as the file tools take it: relative, within the workspace, no option
    private static bool Reads(string command, IServiceProvider services) =>
        command is "pwd" or "ls"
        || (Listing().Match(command) is { Success: true } listing
            && WorkspacePaths.Resolve(WorkspacePaths.Of(services), listing.Groups[1].Value, out _) is not null);

    [GeneratedRegex(@"^ls ([A-Za-z0-9_.][A-Za-z0-9_./-]*)$")]
    private static partial Regex Listing();
}
